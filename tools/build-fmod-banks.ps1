# build-fmod-banks.ps1
# Builds the FMOD Studio banks for the Cathartic Colours FMOD project from the command line.
# Targets Windows PowerShell 5.1.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-fmod-banks.ps1 [-Platforms Desktop,Mobile] [-Diagnostic] [-FmodStudioCl <path>] [-LogDir <dir>]
#
# Runs fmodstudiocl.exe -build on "Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro". Banks are
# written to "Cathartic Colours - FMOD/Build/". Exits with the tool's exit code.
# FMOD skips banks whose build records in .cache/ are current, so a run on an unchanged project
# may write nothing. Don't run it against a project that is open in FMOD Studio.

param(
    [string[]]$Platforms,
    [switch]$Diagnostic,
    [string]$FmodStudioCl = 'C:\Program Files\FMOD SoundSystem\FMOD Studio 2.04.00\fmodstudiocl.exe',
    [string]$LogDir = (Join-Path ([IO.Path]::GetTempPath()) ('fmod-bank-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'Cathartic Colours - FMOD'
$ProjectFile = Join-Path $ProjectDir 'Cathartic Colours - FMOD.fspro'
$BuildDir = Join-Path $ProjectDir 'Build'

if (-not (Test-Path -LiteralPath $FmodStudioCl -PathType Leaf)) {
    Write-Host "fmodstudiocl.exe not found at: $FmodStudioCl" -ForegroundColor Red
    Write-Host 'Install FMOD Studio 2.04.00, or pass the tool location with -FmodStudioCl <path>.' -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $ProjectFile -PathType Leaf)) {
    Write-Host "FMOD project not found at: $ProjectFile" -ForegroundColor Red
    exit 1
}

# Version check: warn only.
try {
    $versionText = (& $FmodStudioCl '-version' 2>&1 | Out-String).Trim()
    Write-Host "Tool version: $versionText"
    if ($versionText -notmatch '2\.04') {
        Write-Warning "Expected FMOD Studio 2.04 but the tool reports '$versionText'. Banks may not match the Unity runtime."
    }
} catch {
    Write-Warning "Could not read the tool version: $($_.Exception.Message)"
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$toolArgs = @('-build')
if ($Diagnostic) { $toolArgs += '-diagnostic' }
if ($Platforms -and $Platforms.Count -gt 0) {
    $toolArgs += '-platforms'
    $toolArgs += ($Platforms -join ',')
}
$toolArgs += '-log-dir'
$toolArgs += $LogDir
$toolArgs += $ProjectFile

$started = Get-Date
Write-Host "Running: fmodstudiocl.exe $($toolArgs -join ' ')"
& $FmodStudioCl @toolArgs | Out-Host
$exitCode = $LASTEXITCODE

Write-Host ''
Write-Host "Exit code: $exitCode"
Write-Host "Log folder: $LogDir"

if (Test-Path -LiteralPath $BuildDir) {
    $written = @(Get-ChildItem -LiteralPath $BuildDir -Recurse -File | Where-Object { $_.LastWriteTime -ge $started })
    if ($written.Count -gt 0) {
        Write-Host 'Bank files written:'
        foreach ($f in $written) {
            Write-Host ('  {0}  ({1:N0} bytes)' -f $f.FullName, $f.Length)
        }
    } else {
        Write-Host 'No bank files changed: the banks were already up to date with the project.'
    }
}

exit $exitCode
