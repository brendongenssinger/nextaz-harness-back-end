param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$previousLocation = Get-Location
$previousAppData = $env:APPDATA
$previousCliHome = $env:DOTNET_CLI_HOME
$previousPackages = $env:NUGET_PACKAGES
try {
    Set-Location -LiteralPath $projectRoot
    $env:APPDATA = Join-Path $projectRoot 'artifacts/appdata'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot 'artifacts/dotnet'
    if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget/packages' }
    $restoreArgs = @('restore', 'NextAZ.Harness.sln', '--configfile', 'NuGet.Config', '--locked-mode')
    if ($Offline) {
        Write-Warning 'Offline: vulnerability audit is not performed in this invocation.'
        $restoreArgs += @('--ignore-failed-sources', '-p:NuGetAudit=false')
    }
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build NextAZ.Harness.sln --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet test NextAZ.Harness.sln --no-build --no-restore -c Release --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
finally {
    $env:APPDATA = $previousAppData
    $env:DOTNET_CLI_HOME = $previousCliHome
    $env:NUGET_PACKAGES = $previousPackages
    Set-Location $previousLocation
}
