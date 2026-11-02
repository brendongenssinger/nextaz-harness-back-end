[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EnvFile,
    [string]$VaultName = "nextaz-kv-prod-70bec273",
    [string]$SecretName = "harness-huggingface-api-key"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI nao encontrado. Instale/autentique o CLI antes de sincronizar secrets."
}

$resolved = [System.IO.Path]::GetFullPath($EnvFile)
if ([System.IO.Path]::GetFileName($resolved) -eq ".env.example") {
    throw "O arquivo de exemplo versionado nunca pode receber secrets."
}
if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
    throw "Arquivo privado de ambiente nao encontrado: $resolved"
}

$null = az account show --only-show-errors 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Azure CLI nao autenticado. Execute az login no terminal seguro."
}

$secret = az keyvault secret show --vault-name $VaultName --name $SecretName `
    --query value -o tsv --only-show-errors
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($secret)) {
    throw "Nao foi possivel ler o segredo '$SecretName' do Key Vault '$VaultName'."
}
$secret = $secret.Trim()
if ($secret.Contains("`r") -or $secret.Contains("`n")) {
    throw "O token do Hugging Face deve ocupar uma unica linha."
}

$key = "Harness__Providers__HuggingFace__ApiKey"
$lines = [System.Collections.Generic.List[string]]::new()
$found = $false
foreach ($line in [System.IO.File]::ReadAllLines($resolved)) {
    if ($line.StartsWith($key + "=", [StringComparison]::Ordinal)) {
        $lines.Add($key + "=" + $secret)
        $found = $true
    }
    else {
        $lines.Add($line)
    }
}
if (-not $found) {
    $lines.Add($key + "=" + $secret)
}

$temporary = $resolved + ".tmp-" + [Guid]::NewGuid().ToString("N")
try {
    [System.IO.File]::WriteAllLines($temporary, $lines,
        [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $resolved -Force
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }
    $secret = $null
}

Write-Output "Token do Hugging Face sincronizado no arquivo privado sem exibir o valor."
