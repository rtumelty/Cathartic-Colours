# build-android.ps1
# Builds Cathartic Colours for Android from the command line (a development APK unless -Profile says otherwise).
# Targets Windows PowerShell 5.1.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-android.ps1 [-Profile <name>] [-VersionCode <n>] [-UnityExe <path>] [-LogDir <dir>]
#
# Runs Unity in batch mode and calls CatharticColours.Build.AndroidBuild.CommandLine, which builds with the
# named build profile: 'Development' (the default, the "Android Development" profile), 'Release' (the "Android"
# profile), or the asset name of any profile in Assets/Settings/Build Profiles. The Android version code
# defaults to the number of commits on the current branch (git rev-list --count HEAD), so every build from a new
# commit has a higher code. The APK or AAB is written to "Cathartic Colours/Builds/Android/". Exits with Unity's
# exit code (0 ok, 1 build failed, 2 bad arguments, 3 build profile missing). Close the Unity editor first: a
# project open in the editor can't be built in batch mode.
# -SkipVersionCode is for testing the failure path only: it leaves -versionCode off the Unity command line.

param(
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe',
    [string]$LogDir = (Join-Path ([IO.Path]::GetTempPath()) ('android-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [Alias('Profile')][string]$BuildProfile = 'Development',
    [int]$VersionCode,
    [switch]$SkipVersionCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'Cathartic Colours'
$LockFile = Join-Path $ProjectDir 'Temp\UnityLockfile'
$ApkDir = Join-Path $ProjectDir 'Builds\Android'

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

if (-not $SkipVersionCode -and -not $PSBoundParameters.ContainsKey('VersionCode')) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host 'git was not found on PATH, so the version code can''t be worked out.' -ForegroundColor Red
        Write-Host 'Install git, or pass -VersionCode <n>.' -ForegroundColor Red
        exit 1
    }
    $countText = (& git -C $RepoRoot rev-list --count HEAD 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $countText -notmatch '^\d+$') {
        Write-Host "git rev-list --count HEAD failed: $countText" -ForegroundColor Red
        exit 1
    }
    $VersionCode = [int]$countText
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
    $unityArgs += @('-versionCode', [string]$VersionCode)
    Write-Host "Version code: $VersionCode"
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
