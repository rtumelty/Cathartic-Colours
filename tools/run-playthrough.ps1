# run-playthrough.ps1
# Runs the PlayMode playthrough tests for test-plan cases (CC-046) and writes a report for people and agents.
# Targets Windows PowerShell 5.1.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\run-playthrough.ps1 (-Case TP-STD-04[,TP-STD-05] | -Area STD | -All) [-UnityExe <path>] [-OutDir <dir>]
#
# Choose exactly one selection:
#   -Case  one or more test-plan case ids, comma-separated or repeated (TP-STD-04, TP-STD-05)
#   -Area  one area code, case-insensitive (MENU, STD, CM, ACM, HUD, OVER, SAVE, AUDIO, ...)
#   -All   every case in the eight automated areas: MENU STD CM ACM HUD OVER SAVE AUDIO
#
# Each run writes to TestResults\playthrough\<yyyyMMdd-HHmmss>\ (or -OutDir):
#   report.md     one line per case in the selection, with its status, details and screenshot links
#   results.xml   Unity's NUnit results
#   unity.log     Unity's log (search it for "error CS" if the tests did not compile)
#   screenshots\  the screenshots the cases saved, named by case id (TP-STD-04-after.png)
#
# Unity runs in batch mode with -runTests and the PlayMode platform, filtered by the NUnit categories of the cases
# (TP-STD-04 is category TP_STD_04, an area is TP_<AREA>). Close the Unity editor first: a project that is open in the
# editor can't be opened in batch mode. Run this in the agent sandbox only. Cases are read from docs/wiki/test-plan.md.
#
# Exit codes: 0 nothing failed; 1 a test failed; 2 usage or setup error, an unknown case id (with nothing failed), a
# project open in Unity, or Unity exited with a code other than 0 or 2 (2 is Unity's code for failed tests).
# Writes the report in every case that gets as far as a selection, so a failure can still be read from report.md.

param(
    [string[]]$Case,
    [string]$Area,
    [switch]$All,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe',
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'Cathartic Colours'
$LockFile = Join-Path $ProjectDir 'Temp\UnityLockfile'
$PlanFile = Join-Path $RepoRoot 'docs\wiki\test-plan.md'
$EditorSettings = 'Cathartic Colours/ProjectSettings/EditorBuildSettings.asset'
$AutomatedAreas = @('MENU', 'STD', 'CM', 'ACM', 'HUD', 'OVER', 'SAVE', 'AUDIO')
$AllCategories = 'TP_MENU;TP_STD;TP_CM;TP_ACM;TP_HUD;TP_OVER;TP_SAVE;TP_AUDIO'

function Show-Usage {
    Write-Host 'Usage: powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\run-playthrough.ps1 (-Case TP-STD-04[,TP-STD-05] | -Area STD | -All) [-UnityExe <path>] [-OutDir <dir>]'
    Write-Host '  -Case  one or more test-plan case ids, e.g. TP-STD-04'
    Write-Host '  -Area  one area code, e.g. STD or MENU'
    Write-Host '  -All   every case in the automated areas: MENU STD CM ACM HUD OVER SAVE AUDIO'
}

# Runs git in the repo and returns its output and exit code. git writes progress to stderr, which 'Stop' would turn
# into an exception, so errors are allowed here.
function Invoke-Git {
    param([string[]]$GitArgs)
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & git -C $RepoRoot @GitArgs 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    $text = (@($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
    return [pscustomobject]@{ Output = $text; Code = $code }
}

# True when the Unity lock file exists and another process holds it open, which means the editor has the project.
function Test-UnityLocked {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Close()
        return $false
    } catch {
        return $true
    }
}

# Escapes text for one markdown table cell: one line, no pipes, short.
function Format-Cell {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $single = ($Text -replace '\s*[\r\n]+\s*', ' ').Trim()
    if ($single.Length -gt 300) { $single = $single.Substring(0, 297) + '...' }
    return $single.Replace('|', '\|')
}

# The first non-empty line of a node's text, or '' when the node is missing.
function Get-FirstLine {
    param($Node)
    if ($null -eq $Node) { return '' }
    foreach ($line in ([string]$Node.InnerText -split "`r?`n")) {
        if ($line.Trim() -ne '') { return $line.Trim() }
    }
    return ''
}

# 1. Test plan: every "### TP-<AREA>-<nn> - <title>" heading and its Automated row.
if (-not (Test-Path -LiteralPath $PlanFile -PathType Leaf)) {
    Write-Host "Test plan not found: $PlanFile" -ForegroundColor Red
    exit 2
}
$plan = New-Object System.Collections.ArrayList
$current = $null
foreach ($line in [IO.File]::ReadAllLines($PlanFile, [Text.Encoding]::UTF8)) {
    if ($line -match '^### (TP-([A-Z]+)-(\d{2})) \u2014 (.+)$') {
        $current = [pscustomobject]@{
            Id = $Matches[1]; Area = $Matches[2]; Title = $Matches[4].Trim()
            Category = 'TP_' + $Matches[2] + '_' + $Matches[3]
            Automated = $false; Unknown = $false; Reason = 'the test plan has no Automated row'
        }
        [void]$plan.Add($current)
    } elseif ($null -ne $current -and $line -match '^\|\s*Automated\s*\|\s*(.*?)\s*\|\s*$') {
        $value = $Matches[1]
        if ($value -match '^`') {
            $current.Automated = $true
            $current.Reason = ''
        } elseif ($value -match '^manual\b[\s\u2014-]*(.*)$') {
            $current.Reason = $Matches[1]
        } else {
            $current.Reason = $value
        }
        $current = $null
    }
}

# 2. Selection, and argument checks.
$caseIds = @()
if ($Case) {
    $caseIds = @($Case | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ -ne '' } | Select-Object -Unique)
}
$hasCase = $caseIds.Count -gt 0
$hasArea = -not [string]::IsNullOrWhiteSpace($Area)
$selectionCount = ([int]$hasCase) + ([int]$hasArea) + ([int][bool]$All)
if ($selectionCount -ne 1) {
    Show-Usage
    exit 2
}

$selected = @()
$selectionText = ''
$areaCode = ''
if ($hasCase) {
    $selectionText = '-Case ' + ($caseIds -join ',')
    $selected = @(foreach ($id in $caseIds) {
        $known = @($plan | Where-Object { $_.Id -eq $id })
        if ($known.Count -gt 0) {
            $known[0]
        } else {
            [pscustomobject]@{ Id = $id; Area = ''; Title = ''; Category = ''; Automated = $false; Unknown = $true; Reason = 'not in the test plan' }
        }
    })
} elseif ($hasArea) {
    $areaCode = $Area.Trim().ToUpperInvariant()
    $selectionText = '-Area ' + $areaCode
    $selected = @($plan | Where-Object { $_.Area -eq $areaCode })
    if ($selected.Count -eq 0) {
        Write-Host "No test-plan cases for area '$areaCode'. Areas are the codes in docs/wiki/test-plan.md, e.g. STD or MENU." -ForegroundColor Red
        exit 2
    }
} else {
    $selectionText = '-All'
    $selected = @($plan | Where-Object { $AutomatedAreas -contains $_.Area })
}

$toRun = @($selected | Where-Object { -not $_.Unknown -and $_.Automated })
$runCategories = ''
if ($toRun.Count -gt 0) {
    if ($hasCase) { $runCategories = (@($toRun | ForEach-Object { $_.Category }) -join ';') }
    elseif ($hasArea) { $runCategories = 'TP_' + $areaCode }
    else { $runCategories = $AllCategories }
}

# 3. Output folder, and the Unity run.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $RepoRoot ('TestResults\playthrough\' + $stamp)
}
$OutDir = [IO.Path]::GetFullPath($OutDir)
$ShotDir = Join-Path $OutDir 'screenshots'
$resultsXml = Join-Path $OutDir 'results.xml'
$unityLog = Join-Path $OutDir 'unity.log'
New-Item -ItemType Directory -Force -Path $ShotDir | Out-Null

$unityRan = $false
$unityExit = $null
$duration = $null
$setupProblem = ''
if ($toRun.Count -gt 0) {
    if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
        Write-Host "Unity.exe not found at: $UnityExe" -ForegroundColor Red
        Write-Host 'Install Unity 6000.6.9f1, or pass the editor location with -UnityExe <path>.' -ForegroundColor Red
        exit 2
    }
    if (Test-UnityLocked -Path $LockFile) {
        Write-Host "The project is open in Unity ($LockFile is in use)." -ForegroundColor Red
        Write-Host 'Close the editor and try again. Batch runs need the project closed.' -ForegroundColor Red
        exit 2
    }

    $unityArgs = @(
        '-batchmode',
        '-projectPath', ('"{0}"' -f $ProjectDir),
        '-runTests',
        '-testPlatform', 'PlayMode',
        '-testCategory', ('"{0}"' -f $runCategories),
        '-testResults', ('"{0}"' -f $resultsXml),
        '-logFile', ('"{0}"' -f $unityLog)
    )
    Write-Host "Running PlayMode tests for $selectionText (categories $runCategories)"
    Write-Host "Output: $OutDir"
    $env:CC_PLAYTHROUGH_REPORT_DIR = $ShotDir
    $started = Get-Date
    try {
        $process = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
        $unityExit = $process.ExitCode
    } finally {
        Remove-Item Env:CC_PLAYTHROUGH_REPORT_DIR -ErrorAction SilentlyContinue
    }
    $duration = (Get-Date) - $started
    $unityRan = $true

    # Batch mode rewrites line endings in this file on some runs. Revert it only when that is all it changed.
    $status = Invoke-Git @('status', '--porcelain', '--', $EditorSettings)
    if ($status.Output -ne '') {
        $diff = Invoke-Git @('diff', '--ignore-cr-at-eol', '--quiet', '--', $EditorSettings)
        if ($diff.Code -eq 0) {
            Invoke-Git @('checkout', '--', $EditorSettings) | Out-Null
            Write-Host 'Reverted line-ending changes to EditorBuildSettings.asset (no other change).'
        }
    }

    if ($unityExit -ne 0 -and $unityExit -ne 2) {
        $setupProblem = "Unity exited with code $unityExit. Search $unityLog for 'error CS' (compile errors) and read the end of the log."
    } elseif (-not (Test-Path -LiteralPath $resultsXml)) {
        $setupProblem = "Unity did not write $resultsXml. Read the end of $unityLog."
    }
}

# 4. Results: every test-case node, grouped by its categories.
$allTestCases = @()
$byCategory = @{}
$runTotals = $null
if (Test-Path -LiteralPath $resultsXml) {
    [xml]$xml = [IO.File]::ReadAllText($resultsXml)
    $runNode = $xml.SelectSingleNode('/test-run')
    if ($null -ne $runNode) {
        $runTotals = [pscustomobject]@{
            Total = $runNode.GetAttribute('total'); Passed = $runNode.GetAttribute('passed')
            Failed = $runNode.GetAttribute('failed'); Skipped = $runNode.GetAttribute('skipped')
        }
    }
    $allTestCases = @($xml.SelectNodes('//test-case'))
    foreach ($tc in $allTestCases) {
        $categories = @($tc.SelectNodes("properties/property[@name='Category']") | ForEach-Object { $_.GetAttribute('value') })
        foreach ($category in $categories) {
            if (-not $byCategory.ContainsKey($category)) { $byCategory[$category] = @() }
            $byCategory[$category] += $tc
        }
    }
}

# Status of one test-case node. Worst first: FAIL, NOT RUN, SKIPPED, PASS.
function Get-TestStatus {
    param($TestCase)
    switch ($TestCase.GetAttribute('result')) {
        'Passed' { return 'PASS' }
        'Failed' { return 'FAIL' }
        'Skipped' { return 'SKIPPED' }
        default { return 'NOT RUN' }
    }
}
$rank = @{ 'FAIL' = 0; 'NOT RUN' = 1; 'SKIPPED' = 2; 'PASS' = 3 }

function Get-ScreenshotLinks {
    param([string]$CaseId)
    if (-not (Test-Path -LiteralPath $ShotDir)) { return @() }
    $files = @(Get-ChildItem -LiteralPath $ShotDir -Filter '*.png' | Where-Object { $_.Name.StartsWith($CaseId) } | Sort-Object Name)
    return @($files | ForEach-Object { '[{0}](screenshots/{0})' -f $_.Name })
}

# 5. Rows of the report.
$rows = @()
$counts = @{ 'PASS' = 0; 'FAIL' = 0; 'SKIPPED' = 0; 'NOT RUN' = 0; 'NOT AUTOMATED' = 0; 'UNKNOWN CASE' = 0 }
foreach ($rec in $selected) {
    $links = @()
    $status = ''
    $details = ''
    if ($rec.Unknown) {
        $status = 'UNKNOWN CASE'
        $details = $rec.Reason
    } elseif (-not $rec.Automated) {
        $status = 'NOT AUTOMATED'
        $details = $rec.Reason
    } else {
        $links = @(Get-ScreenshotLinks -CaseId $rec.Id)
        $caseNodes = @()
        if ($byCategory.ContainsKey($rec.Category)) { $caseNodes = @($byCategory[$rec.Category]) }
        if ($caseNodes.Count -eq 0) {
            $status = 'NOT RUN'
            $details = if ($unityRan) { 'no result in results.xml' } else { 'not run' }
        } else {
            $worst = $null
            foreach ($tc in $caseNodes) {
                $s = Get-TestStatus -TestCase $tc
                if ($null -eq $worst -or $rank[$s] -lt $rank[$worst.Status]) { $worst = [pscustomobject]@{ Status = $s; Node = $tc } }
            }
            $status = $worst.Status
            if ($status -eq 'FAIL') { $details = Get-FirstLine -Node $worst.Node.SelectSingleNode('failure/message') }
            elseif ($status -eq 'SKIPPED') { $details = Get-FirstLine -Node $worst.Node.SelectSingleNode('reason/message') }
        }
    }
    $counts[$status] = $counts[$status] + 1
    $cell = (@(Format-Cell -Text $details) + $links) -join ' '
    $rows += ('| {0} | {1} | {2} | {3} |' -f $rec.Id, (Format-Cell -Text $rec.Title), $status, $cell.Trim())
}

# Failed tests that are not test-plan cases.
$planCategories = @($plan | ForEach-Object { $_.Category })
$otherFailures = @()
foreach ($tc in $allTestCases) {
    if ($tc.GetAttribute('result') -ne 'Failed') { continue }
    $categories = @($tc.SelectNodes("properties/property[@name='Category']") | ForEach-Object { $_.GetAttribute('value') })
    if (@($categories | Where-Object { $planCategories -contains $_ }).Count -eq 0) {
        $otherFailures += ('- `{0}`: {1}' -f $tc.GetAttribute('fullname'), (Format-Cell -Text (Get-FirstLine -Node $tc.SelectSingleNode('failure/message'))))
    }
}

# 6. The report.
$failedCount = $counts['FAIL'] + $otherFailures.Count
$totalsText = 'not available (Unity did not write results)'
if ($null -ne $runTotals) {
    $totalsText = 'passed {0}, failed {1}, skipped {2} (of {3} tests in the run)' -f $runTotals.Passed, $runTotals.Failed, $runTotals.Skipped, $runTotals.Total
}
$unityText = if ($unityRan) { [string]$unityExit } else { 'not run (no automated case selected)' }
$durationText = if ($null -ne $duration) { '{0:hh\:mm\:ss}' -f $duration } else { 'not run' }
$branch = (Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')).Output
$commit = (Invoke-Git @('rev-parse', '--short', 'HEAD')).Output

$report = @()
$report += '# Playthrough report'
$report += ''
$report += "- Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$report += "- Branch: $branch (commit $commit)"
$report += "- Selection: $selectionText"
$report += "- Unity exit code: $unityText"
$report += "- Totals: $totalsText"
$report += "- Duration: $durationText"
$report += '- Results: [results.xml](results.xml), Log: [unity.log](unity.log), Screenshots: [screenshots](screenshots/)'
if ($setupProblem -ne '') { $report += "- Problem: $setupProblem" }
$report += ''
$report += '| Case | Title | Result | Details |'
$report += '|---|---|---|---|'
$report += $rows
$report += ''
$report += '## Failed tests that are not test-plan cases'
$report += ''
if ($otherFailures.Count -eq 0) { $report += 'None.' } else { $report += $otherFailures }
$report += ''

$reportPath = Join-Path $OutDir 'report.md'
[IO.File]::WriteAllText($reportPath, (($report -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "Report: $reportPath"
Write-Host ("Summary: {0} cases: {1} passed, {2} failed, {3} skipped, {4} not run, {5} not automated, {6} unknown. Other failed tests: {7}." -f $selected.Count, $counts['PASS'], $counts['FAIL'], $counts['SKIPPED'], $counts['NOT RUN'], $counts['NOT AUTOMATED'], $counts['UNKNOWN CASE'], $otherFailures.Count)

if ($setupProblem -ne '') {
    Write-Host $setupProblem -ForegroundColor Red
    exit 2
}
if ($failedCount -gt 0) {
    Write-Host 'Some tests failed. Read the report and the screenshots.' -ForegroundColor Red
    exit 1
}
if ($counts['UNKNOWN CASE'] -gt 0) {
    Write-Host 'Unknown case id(s) given. They are listed as UNKNOWN CASE in the report.' -ForegroundColor Red
    exit 2
}
exit 0
