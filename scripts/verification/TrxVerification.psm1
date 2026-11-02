#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TrxCounter {
    param(
        [Parameter(Mandatory)][System.Xml.XmlElement]$Counters,
        [Parameter(Mandatory)][string]$Name
    )
    $attribute = $Counters.GetAttribute($Name)
    $value = 0
    if ([string]::IsNullOrWhiteSpace($attribute) -or
        -not [int]::TryParse($attribute, [ref]$value) -or $value -lt 0) {
        throw [IO.InvalidDataException]::new("TRX counter '$Name' is missing or invalid.")
    }
    return $value
}

function Get-TrxVerificationResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ProjectName
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw [IO.FileNotFoundException]::new("TRX file was not produced for '$ProjectName'.", $Path)
    }
    try {
        [xml]$document = Get-Content -Raw -LiteralPath $Path
    }
    catch {
        throw [IO.InvalidDataException]::new("TRX file for '$ProjectName' is malformed.", $_.Exception)
    }
    $summary = $document.SelectSingleNode("//*[local-name()='ResultSummary']")
    $counters = $document.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $summary -or $null -eq $counters) {
        throw [IO.InvalidDataException]::new("TRX file for '$ProjectName' has no ResultSummary/Counters.")
    }

    $total = Get-TrxCounter -Counters $counters -Name 'total'
    $executed = Get-TrxCounter -Counters $counters -Name 'executed'
    $passed = Get-TrxCounter -Counters $counters -Name 'passed'
    $failed = Get-TrxCounter -Counters $counters -Name 'failed'
    $counterNotExecuted = Get-TrxCounter -Counters $counters -Name 'notExecuted'
    $outcome = $summary.GetAttribute('outcome')
    [System.Xml.XmlElement[]]$resultNodes = @($document.SelectNodes("//*[local-name()='UnitTestResult']"))
    [System.Xml.XmlElement[]]$passedResults = @($resultNodes |
        Where-Object { $_.GetAttribute('outcome') -eq 'Passed' })
    [System.Xml.XmlElement[]]$failedResults = @($resultNodes |
        Where-Object { $_.GetAttribute('outcome') -eq 'Failed' })
    [System.Xml.XmlElement[]]$skippedResults = @($resultNodes |
        Where-Object { $_.GetAttribute('outcome') -in @('NotExecuted', 'Skipped') })
    [System.Xml.XmlElement[]]$unsupportedResults = @($resultNodes |
        Where-Object { $_.GetAttribute('outcome') -notin @('Passed', 'Failed', 'NotExecuted', 'Skipped') })
    [string[]]$skippedTests = @($skippedResults | ForEach-Object { $_.GetAttribute('testName') })
    $skipped = $skippedResults.Count
    $errors = [Collections.Generic.List[string]]::new()
    if ($total -le 0) { $errors.Add('trx_zero_tests') }
    if ($failed -gt 0 -or $failedResults.Count -gt 0) { $errors.Add('trx_failed_tests') }
    if ($outcome -ne 'Completed') { $errors.Add('trx_run_not_completed') }
    if ($resultNodes.Count -ne $total) { $errors.Add('trx_result_count_mismatch') }
    if ($passedResults.Count -ne $passed) { $errors.Add('trx_passed_result_mismatch') }
    if ($failedResults.Count -ne $failed) { $errors.Add('trx_failed_result_mismatch') }
    if (($passedResults.Count + $failedResults.Count) -ne $executed) { $errors.Add('trx_executed_result_mismatch') }
    if ($unsupportedResults.Count -gt 0) { $errors.Add('trx_unsupported_result_outcome') }
    if ($total -ne ($executed + $skipped)) { $errors.Add('trx_total_counter_mismatch') }

    return [pscustomobject]@{
        Project = $ProjectName
        Outcome = $outcome
        Total = $total
        Executed = $executed
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        CounterNotExecuted = $counterNotExecuted
        SkippedTests = @($skippedTests)
        ValidationErrors = @($errors)
    }
}

function Test-TrxSkipPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Reports,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$AllowedSkippedTests,
        [switch]$RequireExactAllowedSkips
    )

    [string[]]$actual = @($Reports | ForEach-Object { $_.SkippedTests } | Sort-Object -Unique)
    [string[]]$allowed = @($AllowedSkippedTests | Sort-Object -Unique)
    [string[]]$unexpected = @($actual | Where-Object { $_ -notin $allowed })
    [string[]]$missing = @(if ($RequireExactAllowedSkips) {
        $allowed | Where-Object { $_ -notin $actual }
    })
    [string[]]$errors = @()
    if ($unexpected.Count -gt 0) { $errors += 'unexpected_skipped_tests' }
    if ($missing.Count -gt 0) { $errors += 'expected_skipped_tests_missing' }

    return [pscustomobject]@{
        IsValid = $errors.Count -eq 0
        Errors = @($errors)
        ActualSkippedTests = @($actual)
        UnexpectedSkippedTests = @($unexpected)
        MissingExpectedSkippedTests = @($missing)
    }
}

Export-ModuleMember -Function Get-TrxVerificationResult, Test-TrxSkipPolicy
