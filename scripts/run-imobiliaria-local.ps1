param(
    [switch]$ApplyMigrations
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repositoryRoot
$productRoot = Join-Path $workspaceRoot 'nextaz-imobiliaria'
$integrationFile = Join-Path $productRoot '.local\integration-settings.json'
$databaseFile = Join-Path $productRoot '.local\postgres-test.json'

if (-not (Test-Path -LiteralPath $integrationFile) -or -not (Test-Path -LiteralPath $databaseFile)) {
    throw 'Local integration settings are missing. Use ignored .local files; never add secrets to source control.'
}

$integration = Get-Content -LiteralPath $integrationFile -Raw | ConvertFrom-Json
$database = Get-Content -LiteralPath $databaseFile -Raw | ConvertFrom-Json
$required = @($integration.TenantId, $integration.HarnessSecret, $database.Username,
    $database.Password, $database.Database, $database.Port)
if ($required | Where-Object { [string]::IsNullOrWhiteSpace([string]$_) }) {
    throw 'Local integration settings are incomplete.'
}

$connectionString = "Host=127.0.0.1;Port=$($database.Port);Database=$($database.Database);Username=$($database.Username);Password=$($database.Password);Include Error Detail=false"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5088'
$env:Authentication__Authority = 'http://localhost:5090'
$env:Authentication__Audience = 'nextaz-harness-api'
$env:ConnectionStrings__Harness = $connectionString
$env:Harness__Persistence__Provider = 'PostgreSql'
$env:Harness__Simulation__Enabled = 'true'
$env:Harness__Simulation__TenantIds__0 = [string]$integration.TenantId
$env:Harness__Imobiliaria__Enabled = 'true'
$env:Harness__Imobiliaria__TenantIds__0 = [string]$integration.TenantId
$env:Harness__Imobiliaria__Integration__Enabled = 'true'
$env:Harness__Imobiliaria__Integration__ApiBaseUrl = 'http://localhost:5098/'
$env:Harness__Imobiliaria__Integration__TokenEndpoint = 'http://localhost:5090/connect/token'
$env:Harness__Imobiliaria__Integration__ClientId = 'nextaz-harness'
$env:Harness__Imobiliaria__Integration__ClientSecret = [string]$integration.HarnessSecret
$env:Harness__Imobiliaria__Integration__Scope = 'imobiliaria.tools'
$env:Harness__Providers__OpenAI__Enabled = 'false'
Set-Item -LiteralPath 'Env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command' -Value 'Warning'

Push-Location $repositoryRoot
try {
    if ($ApplyMigrations) {
        dotnet ef database update --project src/NextAZ.Harness.Infrastructure --startup-project src/NextAZ.Harness.Infrastructure --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Harness migrations failed.' }
    }
    Write-Host 'Starting Harness at http://127.0.0.1:5088 with explicit local simulation and real Auth/backend tools.'
    dotnet run --project src/NextAZ.Harness.Api --no-restore --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'Harness process failed.' }
}
finally {
    Pop-Location
}
