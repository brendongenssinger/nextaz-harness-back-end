#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modulePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'verification/TrxVerification.psm1'
Import-Module $modulePath -Force
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('nextaz-harness-trx-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$assertions = 0

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    $script:assertions++
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Assert-Throws {
    param([Parameter(Mandatory)][scriptblock]$Action, [Parameter(Mandatory)][string]$Message)
    $script:assertions++
    try { & $Action }
    catch { return }
    throw "Assertion failed: $Message"
}

function Write-TrxFixture {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$Total,
        [Parameter(Mandatory)][int]$Executed,
        [Parameter(Mandatory)][int]$Passed,
        [Parameter(Mandatory)][int]$Failed,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$SkippedTests,
        [string]$Outcome = 'Completed'
    )
    $results = @()
    for ($index = 0; $index -lt $Passed; $index++) {
        $results += "<UnitTestResult testName='Passed.$index' outcome='Passed' />"
    }
    for ($index = 0; $index -lt $Failed; $index++) {
        $results += "<UnitTestResult testName='Failed.$index' outcome='Failed' />"
    }
    foreach ($name in $SkippedTests) {
        $results += "<UnitTestResult testName='$name' outcome='NotExecuted' />"
    }
    $xml = @"
<TestRun>
  <Results>$($results -join '')</Results>
  <ResultSummary outcome='$Outcome'>
    <Counters total='$Total' executed='$Executed' passed='$Passed' failed='$Failed' notExecuted='$($SkippedTests.Count)' />
  </ResultSummary>
</TestRun>
"@
    Set-Content -LiteralPath $Path -Value $xml -Encoding utf8
}

try {
    $validPath = Join-Path $testRoot 'valid.trx'
    Write-TrxFixture -Path $validPath -Total 2 -Executed 2 -Passed 2 -Failed 0 -SkippedTests @()
    $valid = Get-TrxVerificationResult -Path $validPath -ProjectName 'Valid.Tests'
    Assert-True ($valid.Total -eq 2 -and $valid.ValidationErrors.Count -eq 0) 'valid TRX must pass'

    Assert-Throws { Get-TrxVerificationResult -Path (Join-Path $testRoot 'missing.trx') -ProjectName 'Missing.Tests' } `
        'missing TRX must throw'

    $malformedPath = Join-Path $testRoot 'malformed.trx'
    Set-Content -LiteralPath $malformedPath -Value '<TestRun>' -Encoding utf8
    Assert-Throws { Get-TrxVerificationResult -Path $malformedPath -ProjectName 'Malformed.Tests' } `
        'malformed TRX must throw'

    $zeroPath = Join-Path $testRoot 'zero.trx'
    Write-TrxFixture -Path $zeroPath -Total 0 -Executed 0 -Passed 0 -Failed 0 -SkippedTests @()
    $zero = Get-TrxVerificationResult -Path $zeroPath -ProjectName 'Zero.Tests'
    Assert-True ($zero.ValidationErrors -contains 'trx_zero_tests') 'zero-test TRX must fail validation'

    $failedPath = Join-Path $testRoot 'failed.trx'
    Write-TrxFixture -Path $failedPath -Total 1 -Executed 1 -Passed 0 -Failed 1 -SkippedTests @()
    $failed = Get-TrxVerificationResult -Path $failedPath -ProjectName 'Failed.Tests'
    Assert-True ($failed.ValidationErrors -contains 'trx_failed_tests') 'failed-test TRX must fail validation'

    $truncatedPath = Join-Path $testRoot 'truncated.trx'
    Set-Content -LiteralPath $truncatedPath -Encoding utf8 -Value @'
<TestRun>
  <Results><UnitTestResult testName="Only.One" outcome="Passed" /></Results>
  <ResultSummary outcome="Completed">
    <Counters total="3" executed="3" passed="3" failed="0" notExecuted="0" />
  </ResultSummary>
</TestRun>
'@
    $truncated = Get-TrxVerificationResult -Path $truncatedPath -ProjectName 'Truncated.Tests'
    Assert-True ($truncated.ValidationErrors -contains 'trx_result_count_mismatch' -and
        $truncated.ValidationErrors -contains 'trx_passed_result_mismatch') `
        'TRX counters must be confronted with individual results'

    $allowed = @('Db.One', 'Db.Two', 'Db.Three')
    $partialPath = Join-Path $testRoot 'partial.trx'
    Write-TrxFixture -Path $partialPath -Total 4 -Executed 1 -Passed 1 -Failed 0 -SkippedTests $allowed
    $partial = Get-TrxVerificationResult -Path $partialPath -ProjectName 'Partial.Tests'
    $allowedPolicy = Test-TrxSkipPolicy -Reports @($partial) -AllowedSkippedTests $allowed -RequireExactAllowedSkips
    Assert-True $allowedPolicy.IsValid 'the exact declared PostgreSQL skips must be accepted'

    $unexpectedPolicy = Test-TrxSkipPolicy -Reports @($partial) -AllowedSkippedTests @('Db.One') -RequireExactAllowedSkips
    Assert-True (-not $unexpectedPolicy.IsValid -and
        $unexpectedPolicy.Errors -contains 'unexpected_skipped_tests') 'unexpected skips must fail'

    $missingPolicy = Test-TrxSkipPolicy -Reports @($partial) -AllowedSkippedTests ($allowed + 'Db.Four') -RequireExactAllowedSkips
    Assert-True (-not $missingPolicy.IsValid -and
        $missingPolicy.Errors -contains 'expected_skipped_tests_missing') 'missing declared skips must fail'

    Write-Host "TRX parser tests passed: $assertions assertions."
}
finally {
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
        [IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $expectedPrefix = Join-Path $resolvedTempRoot 'nextaz-harness-trx-'
    if (-not [IO.Path]::IsPathFullyQualified($resolvedTestRoot) -or
        -not $resolvedTestRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to recursively delete unexpected test path: $resolvedTestRoot"
    }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
}
