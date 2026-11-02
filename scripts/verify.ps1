#Requires -Version 7.0

param(
    [switch]$Offline,
    [switch]$RequirePostgres,
    [switch]$AcknowledgeDisposablePostgres
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $PSScriptRoot 'verification/TrxVerification.psm1') -Force

$runId = '{0}-{1}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ'),
    ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$runDirectory = Join-Path $projectRoot "artifacts/test-results/$runId"
$summaryPath = Join-Path $runDirectory 'summary.json'
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$profile = if ($RequirePostgres) { 'postgres' } else { 'local' }
$summary = [ordered]@{
    schemaVersion = 1
    runId = $runId
    profile = $profile
    result = 'failed'
    exitCode = 1
    startedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    finishedAtUtc = $null
    phases = [ordered]@{
        preflight = [ordered]@{ status = 'pending'; errors = @() }
        restore = [ordered]@{ status = 'not_run'; exitCode = $null }
        build = [ordered]@{ status = 'not_run'; exitCode = $null }
        dependencyAudit = [ordered]@{
            status = if ($Offline) { 'not_run' } else { 'not_verified' }
            mode = if ($Offline) { 'offline_disabled' } else { 'restore_default_settings' }
            reason = if ($Offline) {
                'NuGetAudit was explicitly disabled.'
            }
            else {
                'Restore output is not parsed as an independent vulnerability-audit verdict.'
            }
        }
        tests = [ordered]@{
            status = 'not_run'
            total = 0
            passed = 0
            failed = 0
            skipped = 0
            projects = @()
            policyErrors = @()
        }
    }
}

function Save-Summary {
    $summary.finishedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & dotnet @Arguments | Out-Host
    return $LASTEXITCODE
}

$previousLocation = Get-Location
$previousAppData = $env:APPDATA
$previousCliHome = $env:DOTNET_CLI_HOME
$previousPackages = $env:NUGET_PACKAGES
$hadPostgresSetting = Test-Path Env:HARNESS_POSTGRES_TEST
$previousPostgresSetting = $env:HARNESS_POSTGRES_TEST
$exitCode = 1

try {
    Set-Location -LiteralPath $projectRoot
    $env:APPDATA = Join-Path $projectRoot 'artifacts/appdata'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot 'artifacts/dotnet'
    if (-not $env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget/packages'
    }

    if ($RequirePostgres) {
        if (-not $AcknowledgeDisposablePostgres) {
            $summary.phases.preflight.status = 'failed'
            $summary.phases.preflight.errors = @('disposable_postgres_not_acknowledged')
            throw 'RequirePostgres also needs -AcknowledgeDisposablePostgres because the tests run migrations.'
        }
        if ([string]::IsNullOrWhiteSpace($env:HARNESS_POSTGRES_TEST)) {
            $summary.phases.preflight.status = 'failed'
            $summary.phases.preflight.errors = @('postgres_connection_not_configured')
            throw 'RequirePostgres needs HARNESS_POSTGRES_TEST before any dotnet command is run.'
        }
    }
    else {
        # PostgresPersistenceTests call MigrateAsync. Local verification must never inherit a live connection.
        Remove-Item Env:HARNESS_POSTGRES_TEST -ErrorAction SilentlyContinue
    }
    $summary.phases.preflight.status = 'passed'

    $restoreArgs = @('restore', 'NextAZ.Harness.sln', '--configfile', 'NuGet.Config', '--locked-mode')
    if ($Offline) {
        Write-Warning 'Offline verification: NuGet vulnerability audit is not performed.'
        $restoreArgs += @('--ignore-failed-sources', '-p:NuGetAudit=false')
    }
    $restoreExit = Invoke-DotNet -Arguments $restoreArgs
    $summary.phases.restore.exitCode = $restoreExit
    $summary.phases.restore.status = if ($restoreExit -eq 0) { 'passed' } else { 'failed' }
    if ($restoreExit -ne 0) { throw 'Restore failed.' }

    $buildExit = Invoke-DotNet -Arguments @(
        'build', 'NextAZ.Harness.sln', '--no-restore', '--configuration', 'Release'
    )
    $summary.phases.build.exitCode = $buildExit
    $summary.phases.build.status = if ($buildExit -eq 0) { 'passed' } else { 'failed' }
    if ($buildExit -ne 0) { throw 'Build failed.' }

    $testProjects = @(
        'tests/NextAZ.Harness.Domain.Tests/NextAZ.Harness.Domain.Tests.csproj',
        'tests/NextAZ.Harness.Application.Tests/NextAZ.Harness.Application.Tests.csproj',
        'tests/NextAZ.Harness.Runtime.Tests/NextAZ.Harness.Runtime.Tests.csproj',
        'tests/NextAZ.Harness.Infrastructure.Tests/NextAZ.Harness.Infrastructure.Tests.csproj',
        'tests/NextAZ.Harness.IntegrationTests/NextAZ.Harness.IntegrationTests.csproj'
    )
    $reports = @()
    $testCommandFailed = $false
    foreach ($project in $testProjects) {
        $projectName = [IO.Path]::GetFileNameWithoutExtension($project)
        $projectDirectory = Join-Path $runDirectory $projectName
        $trxPath = Join-Path $projectDirectory "$projectName.trx"
        New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
        $testExit = Invoke-DotNet -Arguments @(
            'test', $project, '--no-build', '--no-restore', '--configuration', 'Release',
            '--logger', "trx;LogFileName=$projectName.trx", '--results-directory', $projectDirectory
        )
        if ($testExit -ne 0) { $testCommandFailed = $true }
        try {
            $report = Get-TrxVerificationResult -Path $trxPath -ProjectName $projectName
            $report | Add-Member -NotePropertyName CommandExitCode -NotePropertyValue $testExit
            $report | Add-Member -NotePropertyName RelativePath -NotePropertyValue (
                [IO.Path]::GetRelativePath($projectRoot, $trxPath).Replace('\', '/'))
            $reports += $report
        }
        catch {
            $reports += [pscustomobject]@{
                Project = $projectName
                RelativePath = [IO.Path]::GetRelativePath($projectRoot, $trxPath).Replace('\', '/')
                CommandExitCode = $testExit
                Outcome = 'Invalid'
                Total = 0
                Executed = 0
                Passed = 0
                Failed = 0
                Skipped = 0
                SkippedTests = @()
                ValidationErrors = @('trx_unreadable')
            }
        }
    }

    $allowedLocalSkips = @(
        'NextAZ.Harness.Infrastructure.Tests.PostgresPersistenceTests.PersistsImmutableFingerprintForConflictingReplay',
        'NextAZ.Harness.Infrastructure.Tests.PostgresPersistenceTests.ExpiredLeaseIsReclaimedAndFencesStaleWriter',
        'NextAZ.Harness.Infrastructure.Tests.PostgresPersistenceTests.TerminalStateAndAuditRollbackTogetherWhenAuditInsertFails',
        'NextAZ.Harness.Infrastructure.Tests.PostgresPersistenceTests.PoliciesCoexistAndPublishedRouteChainIsImmutable'
    )
    $skipPolicy = if ($RequirePostgres) {
        Test-TrxSkipPolicy -Reports $reports -AllowedSkippedTests @() -RequireExactAllowedSkips
    }
    else {
        Test-TrxSkipPolicy -Reports $reports -AllowedSkippedTests $allowedLocalSkips -RequireExactAllowedSkips
    }

    $summary.phases.tests.total = ($reports | Measure-Object -Property Total -Sum).Sum
    $summary.phases.tests.passed = ($reports | Measure-Object -Property Passed -Sum).Sum
    $summary.phases.tests.failed = ($reports | Measure-Object -Property Failed -Sum).Sum
    $summary.phases.tests.skipped = ($reports | Measure-Object -Property Skipped -Sum).Sum
    $summary.phases.tests.projects = @($reports | ForEach-Object {
        [ordered]@{
            project = $_.Project
            trx = $_.RelativePath
            commandExitCode = $_.CommandExitCode
            outcome = $_.Outcome
            total = $_.Total
            executed = $_.Executed
            passed = $_.Passed
            failed = $_.Failed
            skipped = $_.Skipped
            skippedTests = @($_.SkippedTests)
            validationErrors = @($_.ValidationErrors)
        }
    })
    $summary.phases.tests.policyErrors = @($skipPolicy.Errors)

    $trxFailed = @($reports | Where-Object { $_.ValidationErrors.Count -gt 0 }).Count -gt 0
    if ($testCommandFailed -or $trxFailed -or -not $skipPolicy.IsValid) {
        $summary.phases.tests.status = 'failed'
        throw 'Test execution or TRX verification failed.'
    }

    if ($RequirePostgres) {
        $summary.phases.tests.status = 'passed'
        $summary.result = 'passed'
    }
    else {
        $summary.phases.tests.status = 'partial'
        $summary.result = 'partial'
    }
    $summary.exitCode = 0
    $exitCode = 0
}
catch {
    if ($summary.phases.preflight.status -eq 'pending') {
        $summary.phases.preflight.status = 'failed'
        $summary.phases.preflight.errors = @('verification_setup_failed')
    }
    if ($summary.result -ne 'partial' -and $summary.result -ne 'passed') {
        $summary.result = 'failed'
        $summary.exitCode = 1
    }
    Write-Error $_.Exception.Message -ErrorAction Continue
}
finally {
    Save-Summary
    $env:APPDATA = $previousAppData
    $env:DOTNET_CLI_HOME = $previousCliHome
    $env:NUGET_PACKAGES = $previousPackages
    if ($hadPostgresSetting) {
        $env:HARNESS_POSTGRES_TEST = $previousPostgresSetting
    }
    else {
        Remove-Item Env:HARNESS_POSTGRES_TEST -ErrorAction SilentlyContinue
    }
    Set-Location $previousLocation
}

Write-Host "Verification result: $($summary.result.ToUpperInvariant())"
Write-Host "Machine-readable summary: $summaryPath"
exit $exitCode
