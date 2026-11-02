[CmdletBinding()]
param(
    [string]$PythonCommand = "python"
)

$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$virtualEnvironment = Join-Path $sourceRoot ".codex-tmp\hf-receipt-test"
$python = Join-Path $virtualEnvironment "Scripts\python.exe"

& $PythonCommand -c "import torch, PIL; print('PyTorch ' + torch.__version__ + '; Pillow ' + PIL.__version__)"
if ($LASTEXITCODE -ne 0) {
    throw "O Python informado precisa ter PyTorch CPU e Pillow instalados."
}

if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    & $PythonCommand -m venv --system-site-packages $virtualEnvironment
    if ($LASTEXITCODE -ne 0) { throw "Falha ao criar o ambiente Python isolado." }
}

& $python -m pip install --disable-pip-version-check `
    -r (Join-Path $PSScriptRoot "requirements-huggingface-receipt.txt")
if ($LASTEXITCODE -ne 0) { throw "Falha ao instalar as dependencias isoladas." }

Write-Output "Ambiente local preparado em $virtualEnvironment"
