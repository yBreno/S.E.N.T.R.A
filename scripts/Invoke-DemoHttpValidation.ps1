$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$Host.UI.RawUI.WindowTitle = 'SENTRA - Etapa 7.5 - Senha privada'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/etapa-7-5'
[System.IO.Directory]::CreateDirectory($artifacts) | Out-Null
$reportPath = Join-Path $artifacts 'result.json'
$report = [ordered]@{ phase='inspection'; success=$false; database='SENTRA_DEMO_Stage73'; baseUrl='http://127.0.0.1:5180'; startedUtc=[DateTime]::UtcNow.ToString('o') }
$apiProcess = $null
$token = $null
$plain = $null
$secure = $null
$loginBody = $null
$loginResponse = $null
function Save-Report { $report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $reportPath -Encoding UTF8 }
function Require($condition, [string]$message) { if (-not $condition) { throw [InvalidOperationException]::new($message) } }
function Inspect-Demo {
    $text = & dotnet (Join-Path $root 'backend/DemoData.Validation/bin/Debug/net10.0/DemoData.Validation.dll') --inspect
    Require ($LASTEXITCODE -eq 0) 'Inspecao SQL falhou; interrompido.'
    return ($text | ConvertFrom-Json)
}
function Invoke-Local([string]$method, [string]$path, [string]$bearer='', [string]$body='', [string]$origin='http://localhost:5173', [switch]$preflight) {
    $uri = [Uri]('http://127.0.0.1:5180' + $path)
    Require ($uri.Host -eq '127.0.0.1' -and $uri.Port -eq 5180) 'Destino HTTP divergente.'
    $request = [System.Net.HttpWebRequest]::Create($uri)
    $request.Method = $method
    $request.Proxy = $null
    $request.AllowAutoRedirect = $false
    $request.Timeout = 15000
    $request.Headers['Origin'] = $origin
    if ($bearer) { $request.Headers['Authorization'] = 'Bearer ' + $bearer }
    if ($preflight) {
        $request.Headers['Access-Control-Request-Method'] = 'GET'
        $request.Headers['Access-Control-Request-Headers'] = 'authorization'
    }
    if ($body) {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $request.ContentType = 'application/json'
        $request.ContentLength = $bytes.Length
        $stream = $request.GetRequestStream()
        try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose(); [Array]::Clear($bytes, 0, $bytes.Length) }
    }
    $response = $null
    try { $response = $request.GetResponse() }
    catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) { throw [InvalidOperationException]::new('Falha de conexao HTTP local.') }
        $response = $_.Exception.Response
    }
    try {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $json = $null
        if ($text -and $response.ContentType -match 'json') { $json = ConvertFrom-Json -InputObject $text }
        return [pscustomobject]@{ status=[int]$response.StatusCode; json=$json; cors=$response.Headers['Access-Control-Allow-Origin']; auth=$response.Headers['WWW-Authenticate']; utc=[DateTime]::UtcNow.ToString('o') }
    }
    finally { $response.Dispose() }
}
function Valid-Coordinates($row) {
    if ($null -eq $row.latitude -or $null -eq $row.longitude) { return $false }
    if ($row.latitude -is [string] -or $row.longitude -is [string]) { return $false }
    $lat=[double]$row.latitude; $lon=[double]$row.longitude
    return (-not [double]::IsNaN($lat) -and -not [double]::IsInfinity($lat) -and -not [double]::IsNaN($lon) -and -not [double]::IsInfinity($lon) -and $lat -ge -90 -and $lat -le 90 -and $lon -ge -180 -and $lon -le 180)
}
try {
    $env:DOTNET_ENVIRONMENT='Demo'
    $env:ASPNETCORE_ENVIRONMENT='Demo'
    # The sole allowed connection is configured only for this dedicated local process, never read from the API.
    $settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $settings['Data Source']='(localdb)\MSSQLLocalDB'
    $settings['Initial Catalog']='SENTRA_DEMO_Stage73'
    $settings['Integrated Security']=$true
    $settings['Encrypt']=$true
    $settings['TrustServerCertificate']=$true
    $settings['Connect Timeout']=10
    $env:SENTRA_DEMO_CONNECTION_STRING=$settings.ConnectionString
    Remove-Item Env:SENTRA_DEMO_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    $initial = Inspect-Demo
    $report.initial = $initial
    Save-Report
    Require ($initial.database -eq 'SENTRA_DEMO_Stage73' -and $initial.Counts.Users -eq 0) 'Destino divergente ou usuarios existentes; nenhuma criacao autorizada.'
    Require ($initial.Fingerprint -eq 'E2FD78E378604958E760670567A412379C2F7279D8E5F7CD0335EE5F4355A6B3') 'Dados iniciais divergentes da Etapa 7.3.'
    Require ($initial.OperationalFingerprint) 'Fingerprint operacional ausente.'
    Require (-not (Get-NetTCPConnection -State Listen -LocalPort 5180 -ErrorAction SilentlyContinue)) 'Porta 5180 ocupada; interrompido.'
    $config = Get-Content -LiteralPath (Join-Path $root 'backend/Api/appsettings.Demo.json') -Raw | ConvertFrom-Json
    Require ($config.Demo.Enabled -and -not $config.Simulation.Enabled -and -not $config.SeedAdmin.Enabled -and $config.Urls -eq 'http://127.0.0.1:5180') 'Configuracao DEMO divergente.'

    $report.phase='awaiting-admin-password'; Save-Report
    Write-Host 'Etapa 7.5: destino e integridade conferidos; zero usuarios.'
    Write-Host 'Digite a senha apenas neste terminal. Ela nao sera exibida nem enviada ao chat.'
    & dotnet (Join-Path $root 'backend/DemoAdmin/bin/Debug/net10.0/DemoAdmin.dll') --create-admin --confirm SENTRA_DEMO_Stage73
    Require ($LASTEXITCODE -eq 0) 'DemoAdmin nao concluiu; nenhuma repeticao automatica sera realizada.'
    $afterAdmin = Inspect-Demo
    Require ($afterAdmin.Counts.Users -eq 1 -and $afterAdmin.OperationalFingerprint -eq $initial.OperationalFingerprint) 'Contagem ou integridade divergente apos provisionamento.'
    $report.adminCreated=$true
    $report.phase='starting-api'; Save-Report

    $launcher = Join-Path $PSScriptRoot 'Start-DemoApi.ps1'
    $stdout = Join-Path $artifacts 'api.stdout.log'
    $stderr = Join-Path $artifacts 'api.stderr.log'
    $apiProcess = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @('-NoProfile','-File',('"' + $launcher + '"'),'-ConfirmStart') -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $report.apiLauncherPid=$apiProcess.Id
    Save-Report
    $ready=$false
    for ($attempt=0; $attempt -lt 40; $attempt++) {
        if ($apiProcess.HasExited) { throw [InvalidOperationException]::new('Launcher encerrou antes do servidor estar pronto.') }
        $log = Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue
        if ($log -match 'Hosting environment: Demo' -and $log -match 'Now listening on: http://127\.0\.0\.1:5180') { $ready=$true; break }
        Start-Sleep -Milliseconds 500
    }
    Require $ready 'Nao foi possivel confirmar ambiente Demo e listener local.'
    $report.environment='Demo'
    $report.simulationEnabled=$false
    $report.seedersExecuted=$false
    $report.migrationsExecuted=$false
    $report.phase='awaiting-login-password'; Save-Report
    Write-Host 'API DEMO pronta em http://127.0.0.1:5180. Informe novamente a mesma senha para validar o login.'
    $secure = Read-Host 'Senha do Admin DEMO (login)' -AsSecureString
    $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $plain=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $loginBody=@{email='admin.demo@sentra.invalid';password=$plain} | ConvertTo-Json -Compress
    $loginResponse=Invoke-Local 'POST' '/api/auth/login' '' $loginBody
    $report.login=@{status=$loginResponse.status;utc=$loginResponse.utc}
    $plain=$null; $loginBody=$null; $secure.Dispose(); $secure=$null
    Require ($loginResponse.status -eq 200 -and $loginResponse.json.user.role -eq 'Admin' -and $loginResponse.json.token) 'Login nao validado; interrompido.'
    $token=[string]$loginResponse.json.token
    $loginResponse=$null
    $report.phase='http-validation'; Save-Report

    $noToken=Invoke-Local 'GET' '/api/dashboard'
    $badToken=Invoke-Local 'GET' '/api/dashboard' 'invalid'
    $cors=Invoke-Local 'OPTIONS' '/api/dashboard' -preflight
    $deniedCors=Invoke-Local 'OPTIONS' '/api/dashboard' -origin 'https://example.invalid' -preflight
    $blockedWrite=Invoke-Local 'PATCH' '/api/equipments/00000000-0000-0000-0000-000000000000/status' $token '{}'
    $report.security=@{
        missingJwtStatus=$noToken.status;invalidJwtStatus=$badToken.status;
        corsPreflightStatus=$cors.status;corsAllowedOrigin=$cors.cors;
        disallowedOriginStatus=$deniedCors.status;disallowedOriginHasCorsHeader=[bool]$deniedCors.cors;
        writeMethodStatus=$blockedWrite.status;
        role403='Nao testado: somente a conta Admin foi autorizada; nenhum papel ou JWT foi adulterado.'
    }
    Require ($noToken.status -eq 401 -and $badToken.status -eq 401) 'Protecao JWT divergente.'
    Require ($cors.status -eq 204 -and $cors.cors -eq 'http://localhost:5173' -and -not $deniedCors.cors) 'Politica CORS divergente.'
    Require ($blockedWrite.status -eq 405) 'Bloqueio de escrita divergente.'
    $endpoints=New-Object System.Collections.Generic.List[object]
    $dashboard=Invoke-Local 'GET' '/api/dashboard' $token
    $endpoints.Add(@{path='/api/dashboard';status=$dashboard.status;totalEquipments=$dashboard.json.totalEquipments;activeAgents=$dashboard.json.activeAgents;availabilityPercent=$dashboard.json.availabilityRatePercent;utc=$dashboard.utc})
    Require ($dashboard.status -eq 200 -and $dashboard.json.totalEquipments -eq 60 -and $dashboard.json.activeAgents -eq 13) 'Dashboard divergente.'
    foreach ($kind in @('equipments','agents')) {
        $response=Invoke-Local 'GET' ('/api/' + $kind) $token
        $items=@($response.json)
        $expected=if ($kind -eq 'equipments') {60} else {20}
        $valid=@($items | Where-Object { Valid-Coordinates $_ }).Count
        $endpoints.Add(@{path=('/api/' + $kind);status=$response.status;count=$items.Count;validCoordinates=$valid;utc=$response.utc})
        Require ($response.status -eq 200 -and $items.Count -eq $expected -and $valid -eq $expected) 'Registros ou coordenadas divergentes.'
    }
    foreach ($metric in @('Incidents','Failures','Risk')) {
        $path='/api/heatmap?metric=' + $metric
        $response=Invoke-Local 'GET' $path $token
        $points=@($response.json.points)
        $expected=switch ($metric) { 'Incidents' {100} 'Failures' {25} 'Risk' {5} }
        $valid=@($points | Where-Object { (Valid-Coordinates $_) -and $null -ne $_.intensity -and [double]$_.intensity -gt 0 -and [double]$_.intensity -le 1 }).Count
        $endpoints.Add(@{path=$path;status=$response.status;totalPoints=$response.json.totalPoints;validCoordinatesAndIntensity=$valid;utc=$response.utc})
        Require ($response.status -eq 200 -and $response.json.metric -eq $metric -and $response.json.totalPoints -eq $expected -and $valid -eq $expected) 'Heatmap divergente.'
    }
    $report.endpoints=@($endpoints.ToArray())
    # Wait beyond the old worker's initial delay; only SELECT inspections are performed.
    Start-Sleep -Seconds 6
    $final=Inspect-Demo
    $report.final=$final
    Require ($final.Counts.Users -eq 1 -and $final.OperationalFingerprint -eq $initial.OperationalFingerprint) 'Preservacao operacional nao confirmada.'
    $report.operationalRecordsUnchanged=$true
    $report.success=$true
    $report.phase='completed'
    Write-Host 'Validacao concluida. Apenas status, contagens e fingerprints foram registrados.'
    Write-Host 'A API DEMO permanece em http://127.0.0.1:5180; nenhum token ou senha foi salvo.'
}
catch {
    $report.failedPhase=$report.phase
    $report.phase='failed'
    $report.errorType=$_.Exception.GetType().Name
    $report.errorLine=$_.InvocationInfo.ScriptLineNumber
    Write-Host 'Validacao interrompida. Nenhuma credencial sera exibida; consulte o resultado sanitizado.'
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        & taskkill.exe /PID $apiProcess.Id /T /F | Out-Null
        $report.apiStoppedAfterFailure=$true
    }
}
finally {
    $token=$null; $plain=$null; $loginBody=$null; $loginResponse=$null
    if ($null -ne $secure) { $secure.Dispose() }
    $report.finishedUtc=[DateTime]::UtcNow.ToString('o')
    Save-Report
}
