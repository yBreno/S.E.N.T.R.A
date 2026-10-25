[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^SENTRA_DEMO_Avaliacao_[A-Za-z0-9_]+$')]
    [string]$Database,
    [switch]$InteractiveAcceptance
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:SENTRA_DEMO_JWT_KEY)) { throw 'Defina SENTRA_DEMO_JWT_KEY privadamente com 32 bytes aleatorios em Base64.' }
$repoPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$apiProject = Join-Path $repoPath 'backend/Api/Api.csproj'
$values = @{
    DOTNET_ENVIRONMENT = 'Demo'; ASPNETCORE_ENVIRONMENT = 'Demo'; Demo__Enabled = 'true';
    Demo__InteractiveAcceptance = $InteractiveAcceptance.IsPresent.ToString().ToLowerInvariant();
    Simulation__Enabled = 'false'; SeedAdmin__Enabled = 'false'; PredictiveAutomation__Enabled = 'false'; PasswordRecovery__Enabled = 'false';
    SENTRA_DEMO_DATABASE = $Database;
    SENTRA_DEMO_CONNECTION_STRING = ('Server=(localdb)\MSSQLLocalDB;Database=' + $Database + ';Integrated Security=true;TrustServerCertificate=true')
}
$previous = @{}
try {
    foreach ($name in $values.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process'); [Environment]::SetEnvironmentVariable($name, $values[$name], 'Process') }
    Write-Output 'API DEMO local: dados sinteticos; sem carga, seeders, simulacao ou automacao. Aceite pontual somente com opt-in.'
    dotnet run --project $apiProject --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5180
    if ($LASTEXITCODE -ne 0) { throw 'API DEMO encerrada com falha; confira configuracao local.' }
}
finally { foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') } }
