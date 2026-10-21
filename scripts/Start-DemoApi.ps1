param([switch]$ConfirmStart)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmStart) { throw 'Iniciar a API exige autorizacao e o parametro -ConfirmStart.' }
if ([string]::IsNullOrWhiteSpace($env:SENTRA_DEMO_CONNECTION_STRING)) {
    throw 'Defina previamente SENTRA_DEMO_CONNECTION_STRING com a conexao local isolada autorizada, fora do codigo.'
}

# No persistent configuration edits; secret key lives only in this process and the API child.
$previousDemoEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDemoJwt = $env:SENTRA_DEMO_JWT_KEY
$demoRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$demoBytes = New-Object byte[] 32
try {
    $env:DOTNET_ENVIRONMENT = 'Demo'
    $env:ASPNETCORE_ENVIRONMENT = 'Demo'
    $demoRandom.GetBytes($demoBytes)
    $env:SENTRA_DEMO_JWT_KEY = [Convert]::ToBase64String($demoBytes)
    $apiDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'backend/Api'
    Push-Location -LiteralPath $apiDirectory
    try {
        dotnet run --project Api.csproj --no-build --no-launch-profile -- --environment Demo --urls http://127.0.0.1:5180
        if ($LASTEXITCODE -ne 0) { throw 'API DEMO encerrou com erro; nenhuma configuracao persistente foi alterada.' }
    }
    finally { Pop-Location }
}
finally {
    $demoRandom.Dispose()
    [Array]::Clear($demoBytes, 0, $demoBytes.Length)
    $env:DOTNET_ENVIRONMENT = $previousDemoEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspEnvironment
    $env:SENTRA_DEMO_JWT_KEY = $previousDemoJwt
}
