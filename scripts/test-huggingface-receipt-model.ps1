[CmdletBinding(DefaultParameterSetName = "KeyVault")]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ImagePath,

    [Parameter(ParameterSetName = "KeyVault")]
    [string]$VaultName = "nextaz-kv-prod-70bec273",

    [Parameter(ParameterSetName = "KeyVault")]
    [string]$SecretName = "harness-huggingface-api-key",

    [Parameter(Mandatory, ParameterSetName = "Prompt")]
    [switch]$PromptForToken,

    [string]$ModelId = "brendonmascarenhas/train-identify-recibos",

    [ValidateSet("Local", "InferenceProvider")]
    [string]$ExecutionMode = "Local",

    [string]$PythonPath
)

$ErrorActionPreference = "Stop"
$resolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$extension = [System.IO.Path]::GetExtension($resolvedImage).ToLowerInvariant()
$contentType = switch ($extension) {
    ".jpg"  { "image/jpeg" }
    ".jpeg" { "image/jpeg" }
    ".png"  { "image/png" }
    ".webp" { "image/webp" }
    default  { throw "Use uma imagem JPEG, PNG ou WebP." }
}

$token = $null
$bstr = [IntPtr]::Zero
try {
    if ($PSCmdlet.ParameterSetName -eq "Prompt") {
        $secureToken = Read-Host "Token do Hugging Face" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
        $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    else {
        if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
            throw "Azure CLI nao encontrado. Instale/autentique o CLI ou use -PromptForToken."
        }
        $token = az keyvault secret show --vault-name $VaultName --name $SecretName `
            --query value -o tsv --only-show-errors
        if ($LASTEXITCODE -ne 0) {
            throw "Nao foi possivel ler o segredo '$SecretName' do Key Vault '$VaultName'."
        }
    }

    $token = $token.Trim()
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Token do Hugging Face vazio."
    }

    if ($ExecutionMode -eq "Local") {
        if ([string]::IsNullOrWhiteSpace($PythonPath)) {
            $sourceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
            $PythonPath = Join-Path $sourceRoot ".codex-tmp\hf-receipt-test\Scripts\python.exe"
        }
        if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
            throw "Ambiente Python local nao encontrado. Informe -PythonPath ou prepare o ambiente isolado."
        }
        $previousToken = $env:HF_TOKEN
        try {
            $env:HF_TOKEN = $token
            $json = & $PythonPath (Join-Path $PSScriptRoot "test_huggingface_receipt_model.py") `
                --image $resolvedImage --model $ModelId
            if ($LASTEXITCODE -ne 0) {
                throw "A inferencia local do modelo falhou."
            }
            $items = @($json | ConvertFrom-Json)
        }
        finally {
            $env:HF_TOKEN = $previousToken
        }
    }
    else {
        $endpoint = "https://router.huggingface.co/hf-inference/models/$ModelId"
        try {
            $items = @(Invoke-RestMethod -Method Post -Uri $endpoint -InFile $resolvedImage `
                -ContentType $contentType -Headers @{ Authorization = "Bearer $token" } `
                -TimeoutSec 120)
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -in 401, 403) {
                throw "O Hugging Face recusou o token ou a permissao de Inference Providers."
            }
            if ($status -eq 404) {
                throw "O modelo privado nao foi encontrado por esse token ou nao esta disponivel nesse endpoint."
            }
            throw
        }
    }

    $invalidItems = @($items | Where-Object {
        [string]::IsNullOrWhiteSpace($_.label) -or $null -eq $_.score
    })
    if ($items.Count -eq 0 -or $invalidItems.Count -gt 0) {
        throw "O provider retornou um contrato de classificacao invalido."
    }

    Write-Output "Modelo: $ModelId"
    Write-Output "Imagem: $resolvedImage"
    $items | Sort-Object score -Descending | Select-Object `
        @{ Name = "Label"; Expression = { $_.label } },
        @{ Name = "Confianca"; Expression = { "{0:P2}" -f [double]$_.score } } |
        Format-Table -AutoSize
}
finally {
    if ($bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    $token = $null
}
