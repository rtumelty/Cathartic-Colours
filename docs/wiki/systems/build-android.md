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
| `Cathartic Colours/Assets/Settings/Build Profiles/Android™ Development.asset` | The development build profile used by the script by default |
| `Cathartic Colours/Assets/Settings/Build Profiles/Android™ Development (Debuggable).asset` | The same with script debugging on, built with `-Profile Debuggable` (CC-043) |
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

| Profile | `-Profile` | Settings | Use |
|---|---|---|---|
| Android™ Development | `Development` (default) | Development build on, profiler connection on, **script debugging off**, debug symbols **Symbol Table**, APK | Everyday device testing |
| Android™ Development (Debuggable) | `Debuggable` | The same, but script debugging on (wait for managed debugger off) | Only when a C# debugger has to attach on the phone |
| Android™ | `Release` | Unchanged: release settings, symbols Symbol Table, builds an App Bundle | Store release, set up in [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57). Here only for timing comparisons |

The development settings were chosen in [CC-043](https://github.com/rtumelty/Cathartic-Colours/issues/76) ([feature page](../features/CC-043-android-build-times.md#cc-0432--experiments-2026-11-20)):
- **Debug symbols Symbol Table (was Full):** Gradle no longer extracts full native debug data from the IL2CPP library on every changed build. Native crash stacks keep function names but lose file and line numbers.
- **Script debugging off (was on):** IL2CPP no longer adds debugger code to every method. The library drops from 352 MB to 96 MB, and a clean build from about 31 to 14 minutes. The cost is that no C# debugger can attach; use the Debuggable profile for that.
- **Profiler connection on:** it costs nothing measurable, and on-device profiling needs it ([CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29)).

**Getting a debuggable build:** `tools\build-android.ps1 -Profile Debuggable`, then install it as usual. The app starts without waiting for a debugger, and a debugger can attach later ([D3](../features/CC-004-android-dev-builds.md#decisions)). Both development profiles write the same `...-dev.apk` file name. **Switching profiles costs one full C++ rebuild:** the first build after a switch recompiles all IL2CPP C++, about 14 to 30 minutes, and so does switching back.

## Build code
- The build class lives in its own editor-only assembly, `CatharticColours.Build.Editor`, so its EditMode tests can reference it ([D4](../features/CC-004-android-dev-builds.md#decisions)). This follows the `CatharticColours.Simulation` pattern.
- `AndroidBuild.CommandLine` (the script's entry point; `AndroidBuild.Development` is kept and does the same):
  - reads `-versionCode <n>` and the optional `-buildProfile <name>` from the command line;
  - loads the profile from `AndroidBuildArgs.ProfileAssetPath`;
  - sets the version code and calls `BuildPipeline.BuildPlayer` with the profile;
  - restores the previous version code in a `finally` block and saves the assets.
- The profile's own `m_Development` and `m_BuildAppBundle` flags are read through `SerializedObject`, without changing the asset. They name the output `Cathartic Colours/Builds/Android/CatharticColours-<versionName>-<versionCode>-<dev|release>.<apk|aab>` (`AndroidBuildArgs.OutputFileName`). The log line is `APK: <path>` or `AAB: <path>`.
- Exit codes: `0` success, `1` build failed (the build report's errors are logged), `2` bad arguments (missing or non-numeric `-versionCode`, empty `-buildProfile`, a profile name with path characters), `3` build profile not found.
- Only the argument parsing, profile names and file name are unit-tested. The Unity build call is not.

## Running a build
From the repo root, with the Unity editor closed:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-android.ps1
```

Options:
- `-Profile <name>`: the build profile. `Development` (the default) is `Android™ Development`. `Release` is `Android™`, the release profile, which makes an App Bundle (`...-release.aab`, debug-signed when no keystore is set; for timing comparisons only, not for upload). Any other name is the asset name of a profile in `Assets/Settings/Build Profiles/` ([CC-043](https://github.com/rtumelty/Cathartic-Colours/issues/76)).
- `-VersionCode <n>`: use this code instead of the commit count.
- `-UnityExe <path>`: the Unity editor, default `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe`.
- `-LogDir <dir>`: where `unity-build.log` goes, default a new folder under `%TEMP%`.
- `-SkipVersionCode`: leaves `-versionCode` off the Unity command line. Only for testing the failure path (TP-DROID-02).

The script exits with Unity's exit code. It refuses to start if the project looks open in the editor (`Temp/UnityLockfile` exists).

Notes:
- Run it in the agent sandbox only (`CLAUDE.local.md`), never in the manual copy that Ronan has open in Unity.
- **Build times** (CC-043, development profile, warm `Library/` unless noted; see [the feature page](../features/CC-043-android-build-times.md#results)):

  | Build | Before CC-043 | After |
  |---|---|---|
  | Clean (`Library/Bee` deleted) | 31:14 | 13:17 |
  | Unchanged rebuild | 1:05 | 1:11 |
  | One-line C# change | 4:33 | about 1:50 |
  | First build after switching profile | about 30 min | 12:19 back to Development; 28:15 to Debuggable |

  The 17 to 21 minute builds seen before CC-043 followed C# changes on other branches, which made IL2CPP recompile hundreds of C++ files.
- **Windows Defender exclusions** (Ronan's machine, set in CC-043; they save about 25 s per build). Windows Security → Virus & threat protection → Manage settings → Exclusions → Folder:
  - `E:\Projects\Cathartic-Colours\Cathartic Colours\Library`
  - `E:\Projects\Cathartic-Colours\Cathartic Colours\Temp`
  - `%USERPROFILE%\.gradle`
  - `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data\PlaybackEngines\AndroidPlayer`

  Exclusions lower protection for those folders. They are a security setting, so only Ronan changes them.
- **After a code change the APK file can carry dead space** (for example 235 MB for 136 MB of contents). Gradle's incremental packaging leaves the old copy of the replaced library in the file. It installs normally.
- **Unity Services Symbol Upload** (about 7 s per build) runs because Unity's Engine Diagnostics is on. With no Unity Cloud token it uploads nothing. Turning Diagnostics off is project-wide, also affects release, and recompiles all C++, so it's left to [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57). Build profiles also carry their own `m_BuildProfileEngineDiagnosticsState`, which may allow a development-only switch.
- Output: the APK, plus a `_BackUpThisFolder_ButDontShipItWithYourGame` folder holding the IL2CPP symbols. Keep the symbols for crash reports; do not ship them.
- Run the script from Git Bash or a normal terminal. A background PowerShell runner once killed the wrapper (exit 255) while Unity carried on building. Once (CC-043), with output redirected to a file, the script's last `Write-Host` failed with an empty error and the script exited 1 after Unity had exited 0 and written the APK. If that happens, check `Exit code:` in the output and the `APK:` line in the log.

## Reading a slow build
Added by [CC-043](https://github.com/rtumelty/Cathartic-Colours/issues/76) ([feature page](../features/CC-043-android-build-times.md)). Unity's own records are enough to see where the time went. No custom tooling is needed.

| Source | Where | What it shows |
|---|---|---|
| Unity build trace | `Library/Bee/buildreport.json` (overwritten by each build; copy it to keep it) | The whole player build as a timeline: every step Unity, IL2CPP, the C++ compiler, the linker and the post-process tasks take, per thread. Chrome trace format. |
| Bee backend traces | `Library/Bee/backend*.traceevents` (rotating) | The same backend steps, plus why each node ran: `OutputFilesMissingFor <node>` means its output was missing, `CheckInputSignature` that its inputs were checked. |
| Editor log | the `-LogDir` folder, `unity-build.log` | `Android PostProcess task "<name>" took <n> ms` lines (Gradle, symbol upload, moving the output), `Asset Pipeline Refresh ... Total` at startup, and the `Android build Succeeded: ... <time>` summary. |

To open a trace, go to `ui.perfetto.dev` (it runs locally in the browser and uploads nothing) or `chrome://tracing`, and load the file. In the `Unity` process, `Build player` → `Postprocess built player` holds most of the time. Inside it, `Incremental player build` is the IL2CPP and C++ part. `Building Gradle project` is Gradle. Look for `IL2CPP_CodeGen`, `C_Android_arm64` (one per C++ object file compiled), `Link_Android_arm64` and the `CopyFiles` of `libil2cpp.so`.

For a quick text summary of the log:

```
Select-String -Path "<log dir>\unity-build.log" -Pattern 'PostProcess task .* took \d{4,}|Android build Succeeded|InitialRefreshV2'
```

**Not used:** the Build Report Inspector package (`com.unity.build-report-inspector` 0.3.0-preview, from 2022) doesn't compile on Unity 6.6. The analyzer rejects its use of `BinaryFormatter` (error UAC0023). Gradle's online build scans publish to an outside service, so they aren't used either.

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
