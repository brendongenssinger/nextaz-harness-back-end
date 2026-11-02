#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../verification/ProductionEnv.psm1') -Force
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$template = Get-Content -LiteralPath (Join-Path $repository '.env.example') -Raw
$assertions = 0
function Assert-Check([bool]$Condition, [string]$Message) {
    $script:assertions++
    if (-not $Condition) { throw "Falhou: $Message" }
}
function Check([string]$Content) {
    @(Get-HarnessProductionEnvIssues -Lines ($Content -split '\r?\n'))
}
$valid = $template.Replace('CHANGE_ME_HOST_HARNESS', 'harness.test').
    Replace('CHANGE_ME_AUTH_HOST', 'auth.test').Replace('CHANGE_ME_DB_HOST', 'db.test').
    Replace('CHANGE_ME_DB_NAME', 'harness').Replace('CHANGE_ME_DB_USER', 'harness_app').
    Replace('CHANGE_ME_KEY_VAULT', 'nextaz-test-kv').
    Replace('CHANGE_ME_KEY_VERSION', '11111111111111111111111111111111').
    Replace('CHANGE_ME_DB_PASSWORD', 'fake-db-password').Replace('CHANGE_ME_OPENAI_SECRET', 'fake-secret-$raw').
    Replace('CHANGE_ME_MODEL', 'test-model').Replace('CHANGE_ME_TENANT_GUID', '71111111-1111-1111-1111-111111111111').
    Replace('CHANGE_ME_IMOBILIARIA_HOST', 'imobiliaria.test').
    Replace('CHANGE_ME_HARNESS_TO_IMOBILIARIA_SECRET', 'fake-service-secret').
    Replace('Harness__Receipts__Enabled=false', 'Harness__Receipts__Enabled=true')
Assert-Check (@(Check $valid).Count -eq 0) 'fixture completa deve passar sem rede ou banco'
Assert-Check (@(Check $template).Count -gt 0) 'template deve falhar fechado'
Assert-Check (@(Check '').Count -gt 0) 'arquivo vazio'
Assert-Check (@(Check ($valid.Replace('ASPNETCORE_ENVIRONMENT=Production','ASPNETCORE_ENVIRONMENT=Development'))).Count -gt 0) 'Development'
Assert-Check (@(Check ($valid.Replace('Harness__Simulation__Enabled=false','Harness__Simulation__Enabled=true'))).Count -gt 0) 'simulacao'
Assert-Check (@(Check ($valid.Replace('https://auth.test','http://auth.test'))).Count -gt 0) 'issuer HTTP'
Assert-Check (@(Check ($valid.Replace('https://auth.test','https://localhost'))).Count -gt 0) 'issuer loopback'
Assert-Check (@(Check ($valid.Replace('/11111111111111111111111111111111',''))).Count -gt 0) 'Key Vault key sem versao'
Assert-Check (@(Check ($valid.Replace('71111111-1111-1111-1111-111111111111',[Guid]::Empty.ToString()))).Count -gt 0) 'tenant vazio'
Assert-Check (@(Check ($valid + "`nHarness__Receipts__TenantIds__1=71111111-1111-1111-1111-111111111111")).Count -gt 0) 'tenant duplicado'
Assert-Check (@(Check ($valid + "`nASPNETCORE_ENVIRONMENT=Production")).Count -gt 0) 'chave duplicada'
Assert-Check (@(Check ($valid.Replace('harness.test;localhost;127.0.0.1','*'))).Count -gt 0) 'host wildcard'
Assert-Check (@(Check ($valid.Replace('SSL Mode=VerifyFull','SSL Mode=Disable'))).Count -gt 0) 'TLS banco desabilitado'
Assert-Check (@(Check ($valid.Replace('Username=harness_app','Username=postgres'))).Count -gt 0) 'usuario dedicado'
Assert-Check (@(Check ($valid + "`nHarness__EmployeeDraftProfiles__0__Enabled=true")).Count -gt 0) 'piloto employee drafts'
Assert-Check (@(Check ($valid.Replace('Harness__Receipts__Enabled=true','Harness__Receipts__Enabled=false'))).Count -gt 0) 'ao menos um agente operacional'
$validImobiliaria = $valid.Replace('Harness__Receipts__Enabled=true','Harness__Receipts__Enabled=false').
    Replace('Harness__Imobiliaria__Enabled=false','Harness__Imobiliaria__Enabled=true').
    Replace('Harness__Imobiliaria__Integration__Enabled=false','Harness__Imobiliaria__Integration__Enabled=true')
Assert-Check (@(Check $validImobiliaria).Count -eq 0) 'Imobiliaria completa deve passar'
Assert-Check (@(Check ($validImobiliaria.Replace('Harness__Imobiliaria__Integration__Enabled=true','Harness__Imobiliaria__Integration__Enabled=false'))).Count -gt 0) 'Imobiliaria exige integracao'
$invalid = $valid.Replace('ConnectionStrings__Harness=Host=', 'ConnectionStrings__Harness="fake-private-data;Host=')
$safeErrors = @(Check $invalid) -join "`n"
Assert-Check (-not $safeErrors.Contains('fake-private-data') -and -not $safeErrors.Contains('fake-secret-$raw')) 'erros nao expoem valores'
Assert-Check ($null -eq (Get-Item Env:FAKE_HARNESS_ENV_TEST -ErrorAction SilentlyContinue)) 'parser nao exporta env'
Write-Host "Production env: $assertions verificacoes aprovadas; nenhuma conexao externa."
