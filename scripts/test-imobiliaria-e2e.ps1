param(
    [Parameter(Mandatory = $true)]
    [guid]$AtendimentoId
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repositoryRoot
$settingsFile = Join-Path $workspaceRoot 'nextaz-imobiliaria\.local\integration-settings.json'

if (-not (Test-Path -LiteralPath $settingsFile)) {
    throw 'Local integration settings are missing. Use the ignored .local file.'
}

$settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
$required = @($settings.TenantId, $settings.ImobiliariaHarnessSecret)
if ($required | Where-Object { [string]::IsNullOrWhiteSpace([string]$_) }) {
    throw 'TenantId or ImobiliariaHarnessSecret is missing from local integration settings.'
}

$tokenResponse = Invoke-RestMethod -Method Post -Uri 'http://localhost:5090/connect/token' `
    -ContentType 'application/x-www-form-urlencoded' -Body @{
        grant_type = 'client_credentials'
        client_id = 'nextaz-imobiliaria-harness'
        client_secret = [string]$settings.ImobiliariaHarnessSecret
        scope = 'imobiliaria.atendimento'
        tenant_id = [string]$settings.TenantId
    }
if ([string]::IsNullOrWhiteSpace([string]$tokenResponse.access_token)) {
    throw 'Auth did not return an access token.'
}

$correlationId = [guid]::NewGuid().ToString()
$requestBody = @{
    tenantId = [string]$settings.TenantId
    channel = 'api'
    operation = 'imobiliaria.atendimento'
    content = 'Quais imóveis disponíveis podem ser apresentados neste atendimento?'
    correlationId = $correlationId
    metadata = @{
        source = 'nextaz-imobiliaria'
        atendimentoId = $AtendimentoId.ToString()
    }
} | ConvertTo-Json -Depth 5
$headers = @{ Authorization = "Bearer $($tokenResponse.access_token)" }
$execution = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5088/api/v1/imobiliaria/atendimentos' `
    -Headers $headers -ContentType 'application/json' -Body $requestBody

if ($execution.status -ne 'Completed' -or
    -not ([string]$execution.content).StartsWith('[SIMULACAO]', [StringComparison]::Ordinal)) {
    throw "The simulated typed-tool execution did not complete. Status=$($execution.status); Error=$($execution.error.code)"
}

$stored = Invoke-RestMethod -Method Get `
    -Uri "http://127.0.0.1:5088/api/v1/imobiliaria/atendimentos/$($execution.executionId)" `
    -Headers $headers
if ($stored.executionId -ne $execution.executionId -or $stored.status -ne 'Completed') {
    throw 'The persisted execution could not be read back with the same tenant-scoped service identity.'
}

[pscustomobject]@{
    Status = $execution.status
    AgentId = $execution.agentId
    Provider = $execution.providerId
    TypedContextValidated = ([string]$execution.content).Contains('consulta tipada de contexto')
    PersistedReadValidated = $true
    CorrelationMatched = ($execution.correlationId -eq $correlationId)
} | ConvertTo-Json -Compress
