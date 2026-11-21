# build-android.ps1
# Builds Cathartic Colours for Android from the command line (a development APK unless -Profile says otherwise).
# Targets Windows PowerShell 5.1.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-android.ps1 [-Profile <name>] [-VersionCode <n>] [-PrintVersionCode] [-UnityExe <path>] [-LogDir <dir>]
#
# Runs Unity in batch mode and calls CatharticColours.Build.AndroidBuild.CommandLine, which builds with the
# named build profile: 'Development' (the default, the "Android Development" profile), 'Debuggable' (the "Android
# Development (Debuggable)" profile: development build with script debugging on, for attaching a debugger), 'Release'
# (the "Android" profile), or the asset name of any profile in Assets/Settings/Build Profiles.
#
# Version code: PRs are squash-merged into develop, so counting commits on the current branch can give a feature
# build a higher code than a later develop build. Instead the code is  base * 100 + ahead, where base is the commit
# count at the merge base of HEAD and origin/develop and ahead is the number of commits on HEAD that aren't in
# origin/develop (0 on develop). A build on develop always beats one from a branch cut earlier, and builds from a
# branch rise with each commit. ahead must be 0..99; rebase onto or merge develop if the branch is further ahead.
# The script runs 'git fetch origin develop' first; if that fails (offline) it warns and uses the local
# origin/develop. -VersionCode <n> overrides all of this and skips git. -PrintVersionCode prints the base, ahead
# and code, then exits 0 without checking for Unity or starting it.
#
# The APK or AAB is written to "Cathartic Colours/Builds/Android/". Exits with Unity's exit code (0 ok, 1 build
# failed, 2 bad arguments, 3 build profile missing). Close the Unity editor first: a project open in the editor
# can't be built in batch mode.
# -SkipVersionCode is for testing the failure path only: it leaves all version arguments off the Unity command line.

param(
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe',
    [string]$LogDir = (Join-Path ([IO.Path]::GetTempPath()) ('android-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [Alias('Profile')][string]$BuildProfile = 'Development',
    [int]$VersionCode,
    [switch]$PrintVersionCode,
    [switch]$SkipVersionCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'Cathartic Colours'
$LockFile = Join-Path $ProjectDir 'Temp\UnityLockfile'
$ApkDir = Join-Path $ProjectDir 'Builds\Android'

$VersionCodeOverride = $PSBoundParameters.ContainsKey('VersionCode')
$VersionBase = 0
$VersionAhead = 0

if ($VersionCodeOverride) {
    if ($PrintVersionCode) {
        Write-Host "Version code: $VersionCode (override from -VersionCode, git not used)"
        exit 0
    }
} elseif (-not $SkipVersionCode) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host 'git was not found on PATH, so the version code can''t be worked out.' -ForegroundColor Red
        Write-Host 'Install git, or pass -VersionCode <n>.' -ForegroundColor Red
        exit 1
    }

    # git fetch writes progress to stderr even on success, which 'Stop' would turn into an exception.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $fetchOutput = (& git -C $RepoRoot fetch --quiet origin develop 2>&1 | Out-String).Trim()
    $fetchExit = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    if ($fetchExit -ne 0) {
        Write-Host "Couldn't fetch develop from origin (offline?): $fetchOutput" -ForegroundColor Yellow
        Write-Host 'Using the local origin/develop, which may be out of date.' -ForegroundColor Yellow
    }

    & git -C $RepoRoot rev-parse --verify --quiet refs/remotes/origin/develop 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'origin/develop was not found, so the version code can''t be worked out.' -ForegroundColor Red
        Write-Host 'Run "git fetch origin develop", or pass -VersionCode <n>.' -ForegroundColor Red
        exit 1
    }

    $mergeBase = (& git -C $RepoRoot merge-base HEAD refs/remotes/origin/develop 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $mergeBase -notmatch '^[0-9a-fA-F]+$') {
        Write-Host "git merge-base HEAD origin/develop failed: $mergeBase" -ForegroundColor Red
        exit 1
    }

    $baseText = (& git -C $RepoRoot rev-list --count $mergeBase 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $baseText -notmatch '^\d+$') {
        Write-Host "git rev-list --count $mergeBase failed: $baseText" -ForegroundColor Red
        exit 1
    }
    $VersionBase = [int]$baseText

    $aheadText = (& git -C $RepoRoot rev-list --count refs/remotes/origin/develop..HEAD 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $aheadText -notmatch '^\d+$') {
        Write-Host "git rev-list --count origin/develop..HEAD failed: $aheadText" -ForegroundColor Red
        exit 1
    }
    $VersionAhead = [int]$aheadText

    # AndroidBuildArgs.TryComposeVersionCode is the authoritative check; this one fails early with a clear message.
    if ($VersionAhead -gt 99) {
        Write-Host "This branch is $VersionAhead commits ahead of develop, but the version code only has room for 99." -ForegroundColor Red
        Write-Host 'Rebase onto or merge develop, or pass -VersionCode <n>.' -ForegroundColor Red
        exit 1
    }
    $VersionCode = $VersionBase * 100 + $VersionAhead

    if ($PrintVersionCode) {
        Write-Host "Develop base: $VersionBase"
        Write-Host "Commits ahead of develop: $VersionAhead"
        Write-Host "Version code: $VersionCode"
        exit 0
    }
} elseif ($PrintVersionCode) {
    Write-Host 'Nothing to print: -SkipVersionCode leaves the version code off.'
    exit 0
}

if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    Write-Host "Unity.exe not found at: $UnityExe" -ForegroundColor Red
    Write-Host 'Install Unity 6000.6.9f1, or pass the editor location with -UnityExe <path>.' -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $ProjectDir -PathType Container)) {
    Write-Host "Unity project not found at: $ProjectDir" -ForegroundColor Red
    exit 1
}
if (Test-Path -LiteralPath $LockFile) {
    Write-Host "The project looks open in Unity (found $LockFile)." -ForegroundColor Red
    Write-Host 'Close the editor and try again. If no editor is running, delete that file.' -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir 'unity-build.log'

$unityArgs = @(
    '-batchmode', '-quit',
    '-projectPath', ('"{0}"' -f $ProjectDir),
    '-buildTarget', 'Android',
    '-executeMethod', 'CatharticColours.Build.AndroidBuild.CommandLine',
    '-buildProfile', ('"{0}"' -f $BuildProfile)
)
Write-Host "Build profile: $BuildProfile"
if (-not $SkipVersionCode) {
    if ($VersionCodeOverride) {
        $unityArgs += @('-versionCode', [string]$VersionCode)
        Write-Host "Version code: $VersionCode (override from -VersionCode)"
    } else {
        $unityArgs += @('-versionBase', [string]$VersionBase, '-versionAhead', [string]$VersionAhead)
        Write-Host "Version code: $VersionCode (develop base $VersionBase x 100 + $VersionAhead ahead)"
    }
}
$unityArgs += @('-logFile', ('"{0}"' -f $LogFile))

$started = Get-Date
Write-Host "Running: Unity.exe $($unityArgs -join ' ')"
$process = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$exitCode = $process.ExitCode
$elapsed = (Get-Date) - $started

Write-Host ''
Write-Host "Exit code: $exitCode"
Write-Host ('Elapsed: {0:hh\:mm\:ss}' -f $elapsed)
Write-Host "Log: $LogFile"

if ($exitCode -eq 0) {
    $apks = @()
    if (Test-Path -LiteralPath $LogFile) {
        $apks = @(Select-String -LiteralPath $LogFile -Pattern '^(APK|AAB): (.+)$' | ForEach-Object { $_.Matches[0].Groups[2].Value.Trim() })
    }
    if ($apks.Count -eq 0 -and (Test-Path -LiteralPath $ApkDir)) {
        $newest = Get-ChildItem -LiteralPath $ApkDir | Where-Object { $_.Extension -in '.apk', '.aab' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($newest) { $apks = @($newest.FullName) }
    }
    foreach ($apk in $apks) {
        $kind = if ($apk -like '*.aab') { 'AAB' } else { 'APK' }
        Write-Host "${kind}: $apk" -ForegroundColor Green
    }
    if ($apks.Count -eq 0) {
        Write-Host 'Unity reported success but no APK or AAB was found.' -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host 'The build failed. See the log for details.' -ForegroundColor Red
}

exit $exitCode
