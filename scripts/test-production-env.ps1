#Requires -Version 7.0
param([Parameter(Mandatory)][string]$EnvFile)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'verification/ProductionEnv.psm1') -Force
if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    Write-Error 'Arquivo de env nao encontrado. Copie .env.example para um arquivo privado.'
    exit 1
}
$issues = @(Get-HarnessProductionEnvIssues -Lines (Get-Content -LiteralPath $EnvFile))
if ($issues.Count) {
    $issues | ForEach-Object { Write-Host "BLOQUEIO: $_" }
    Write-Host 'Preparacao incompleta. Nenhum deploy, acesso a banco ou chamada de IA foi realizado.'
    exit 1
}
Write-Host 'Env passou na verificacao estatica; valores sensiveis nao foram exibidos.'
Write-Host 'Isto NAO aprova release: validar banco/migrations, Auth, integracoes, DNS/TLS e rollback.'
exit 0
