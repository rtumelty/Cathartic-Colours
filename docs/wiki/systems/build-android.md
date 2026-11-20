# Android build

## Overview
Describes how an Android development APK of Cathartic Colours is built from the command line, what the Android player settings and build profiles are, and how to install the result on a phone. The build is set up by [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10) (see [the feature page](../features/CC-004-android-dev-builds.md)). Store releases (signing, App Bundles, Google Play) are not covered yet; see [Later work](#later-work).

Unity paths are relative to the Unity project folder (`Cathartic Colours/`); repo paths are relative to the repo root.

## Key files
| File | Role |
|---|---|
| `tools/build-android.ps1` | Command-line wrapper: computes the version code, runs Unity in batch mode, prints the APK path |
| `Cathartic Colours/Assets/Editor/Build/AndroidBuild.cs` | Editor entry point `CatharticColours.Build.AndroidBuild.Development` |
| `Cathartic Colours/Assets/Editor/Build/AndroidBuildArgs.cs` | Pure helpers: `-versionCode` parsing and the output file name |
| `Cathartic Colours/Assets/Editor/Build/CatharticColours.Build.Editor.asmdef` | The editor-only assembly `CatharticColours.Build.Editor` |
| `Cathartic Colours/Assets/Tests/EditMode/Build/` | EditMode tests for `AndroidBuildArgs` (assembly `CatharticColours.Build.Editor.Tests`) |
| `Cathartic Colours/Assets/Settings/Build Profiles/Android™ Development.asset` | The development build profile used by the script |
| `Cathartic Colours/Builds/Android/` | Output folder for APKs (git-ignored by `/[Bb]uilds/`) |

## Player settings
Set in `Cathartic Colours/ProjectSettings/ProjectSettings.asset`:

| Setting | Value | Notes |
|---|---|---|
| Application id | `com.catharticcolours.game` | Permanent once the app is on Google Play ([D1](../features/CC-004-android-dev-builds.md#decisions)) |
| Version name | `bundleVersion` = `0.0.1` | Bumped by hand |
| Version code | `git rev-list --count HEAD` | Set on each build and restored afterwards, so the working tree stays clean |
| Minimum API level | 26 | |
| Target API level | highest installed | `AndroidTargetSdkVersion` is 0 |
| Scripting backend | IL2CPP | |
| Target architecture | ARM64 only | |

The version code grows with every commit on the branch, so a newer build always installs over an older one ([D2](../features/CC-004-android-dev-builds.md#decisions)). The version name does not change by itself.

## Build profiles
Profiles are in `Cathartic Colours/Assets/Settings/Build Profiles/`.

| Profile | Settings | Use |
|---|---|---|
| Android™ Development | Development build on, script debugging on, wait for managed debugger off, build App Bundle off (APK) | Device testing; used by the script |
| Android™ | Unchanged; builds an App Bundle | Store release, to be set up in [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57) |

The app starts without waiting for a debugger, but a debugger can still attach later ([D3](../features/CC-004-android-dev-builds.md#decisions)).

## Build code
- The build class lives in its own editor-only assembly, `CatharticColours.Build.Editor`, so its EditMode tests can reference it ([D4](../features/CC-004-android-dev-builds.md#decisions)). This follows the `CatharticColours.Simulation` pattern.
- `AndroidBuild.Development` reads `-versionCode <n>` from the command line, loads the profile, sets the version code and calls `BuildPipeline.BuildPlayer` with the profile. It restores the previous version code in a `finally` block and saves the assets.
- The output path is `Cathartic Colours/Builds/Android/CatharticColours-<versionName>-<versionCode>-dev.apk`, built by `AndroidBuildArgs.OutputFileName`.
- Exit codes: `0` success, `1` build failed (the build report's errors are logged), `2` bad arguments (missing or non-numeric `-versionCode`), `3` build profile not found.
- Only the argument parsing and the file name are unit-tested. The Unity build call is not.

## Running a build
From the repo root, with the Unity editor closed:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-android.ps1
```

Options:
- `-VersionCode <n>`: use this code instead of the commit count.
- `-UnityExe <path>`: the Unity editor, default `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe`.
- `-LogDir <dir>`: where `unity-build.log` goes, default a new folder under `%TEMP%`.
- `-SkipVersionCode`: leaves `-versionCode` off the Unity command line. Only for testing the failure path (TP-DROID-02).

The script exits with Unity's exit code. It refuses to start if the project looks open in the editor (`Temp/UnityLockfile` exists).

Notes:
- Run it in the agent sandbox only (`CLAUDE.local.md`), never in the manual copy that Ronan has open in Unity.
- The first build took about 36 minutes (IL2CPP and Gradle from cold). A rebuild with a warm `Library/` took about 3 minutes.
- Output: the APK, plus a `_BackUpThisFolder_ButDontShipItWithYourGame` folder holding the IL2CPP symbols. Keep the symbols for crash reports; do not ship them.
- Run the script from Git Bash or a normal terminal. A background PowerShell runner once killed the wrapper (exit 255) while Unity carried on building.

## Checking an APK
The Android SDK is installed with Unity at `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK`. To check the package and version code:

```
"<SDK>\build-tools\36.0.0\aapt.exe" dump badging "<apk>"
```

The first line shows `package: name='com.catharticcolours.game' ... versionCode='<n>' versionName='0.0.1'`.

## Installing on a phone
Ronan's phone needs Developer options and USB debugging on, and the phone must accept the "Allow USB debugging" prompt.

```
"<SDK>\platform-tools\adb.exe" devices
"<SDK>\platform-tools\adb.exe" install -r "<apk>"
```

`devices` should list the phone as `device`. `install -r` replaces an older build and keeps its data, so the saved game survives ([TP-DROID-03](../test-plan.md#android-build-droid)).

## FMOD banks
Android builds pack the Mobile banks, `Cathartic Colours - FMOD/Build/Mobile/`, not the Desktop ones. This is set by the `PlatformAndroid` entry in `Assets/Resources/FMOD/FMODStudioSettings.asset` ([CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10)). See [Audio and FMOD](audio-fmod.md#fmod-for-unity-settings) for the settings and the bank layout.

## Working tree after a build
The five settings files that Unity 6.6 re-saved on every build (adding default fields from the upgrade) were committed with their new defaults in [CC-040](https://github.com/rtumelty/Cathartic-Colours/issues/69), so a build now leaves the working tree clean. The Android version code is still set and restored by the build itself.

## Later work
- [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57): release signing (the keystore stays out of git, in local gitignored config), the release profile producing an App Bundle, and Google Play upload. CC-036 also confirms that the commit-count version code works for store uploads.
- [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29): profiling on device, which uses this build.

## Related test cases
- [Android build](../test-plan.md#android-build-droid): TP-DROID-01 to TP-DROID-04.
