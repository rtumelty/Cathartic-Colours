# Audio and FMOD

## Purpose
Describes how the game plays sound: simulation systems create audio event entities carrying an FMOD event path, `AudioSystem` plays them as one-shots, and FMOD for Unity loads banks that were built from the separate FMOD Studio project. Also records what the FMOD Studio project and the built banks contain, and how to rebuild the banks.

Unity paths are relative to the Unity project folder (`Cathartic Colours/`); FMOD Studio paths are relative to the repo root and start with `Cathartic Colours - FMOD/`.

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/ECS/Utilities/FMODEventPaths.cs` | The five event path constants |
| `Assets/Scripts/ECS/Components/AudioEventComponent.cs` | Event entity component (`FixedString64Bytes EventPath`) |
| `Assets/Scripts/ECS/Systems/AudioSystem.cs` | Plays and destroys audio event entities |
| `Assets/Scripts/ECS/Systems/BoardStepSystem.cs` | Emits move, merge and game-over sounds for accepted moves (since CC-006.3) |
| `Packages/com.fmod.fmod-for-unity.v204/` | FMOD for Unity 2.04.00, an embedded UPM package ([ADR-003](../decisions/ADR-003-fmod-embedded-package.md)); was `Assets/Plugins/FMOD/` (2.03.11) before CC-033 |
| `Assets/Resources/FMOD/FMODStudioSettings.asset` | FMOD for Unity settings (the event cache `FMODStudioCache.asset` beside it is gitignored) |
| `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Gameplay scene.unity` | Each has an FMOD `StudioListener` component (on the camera object) |
| `Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro` | FMOD Studio project file |
| `Cathartic Colours - FMOD/Build/Desktop/Master.bank`, `Master.strings.bank` | Built Desktop banks that Unity reads (committed) |
| `Cathartic Colours - FMOD/Build/Mobile/Master.bank`, `Master.strings.bank` | Built Mobile banks (committed) |
| `Cathartic Colours - FMOD/.gitignore` | Ignores FMOD Studio's per-machine folders `.cache/`, `.user/` and `.unsaved/` |
| `tools/build-fmod-banks.ps1` | Rebuilds the banks from the command line ([Rebuilding the banks](#rebuilding-the-banks)) |

## How it works

### Event entities and `AudioSystem`
- `AudioEventComponent` holds one field, `FixedString64Bytes EventPath` (`Assets/Scripts/ECS/Components/AudioEventComponent.cs`).
- `AudioSystem` is `partial struct AudioSystem : ISystem` in `SimulationSystemGroup`, with no ordering attributes and no `RequireForUpdate`. Each frame it queries every `AudioEventComponent`, calls `RuntimeManager.PlayOneShot(path.ToString(), Vector3.zero)` and queues `DestroyEntity` on an `EntityCommandBuffer` (`Assets/Scripts/ECS/Systems/AudioSystem.cs`). All sounds therefore play at the origin; there is no 3D positioning, parameter setting or event-instance handle.
- It is unordered against the systems that create events, so a sound may play in the frame it was created or one frame later ([ecs-frame-order.md](ecs-frame-order.md)).
- `RuntimeManager.PlayOneShot(string, Vector3)` catches `EventNotFoundException` and logs the warning `"[FMOD] Event not found: <path>"` (`Packages/com.fmod.fmod-for-unity.v204/src/RuntimeManager.cs`). Whether an unknown path actually reaches that exception inside `PathToGUID` was not checked, so treat the missing-event behaviour as unverified.

### Event paths and where they are emitted
`Assets/Scripts/ECS/Utilities/FMODEventPaths.cs` (`static class`, each member a `FixedString64Bytes` property):

| Constant | Path | Emitted by |
|---|---|---|
| `BlockMove` | `event:/Block_Move` | `BoardStepSystem`, once per accepted move (a rejected move makes no sound since CC-006.3) |
| `BlockMergeSmall` | `event:/Block_Merge_Small` | `BoardStepSystem.MergeAudioPath` |
| `BlockMergeMedium` | `event:/Block_Merge_Medium` | `BoardStepSystem.MergeAudioPath` |
| `BlockMergeLarge` | `event:/Block_Merge_Large` | `BoardStepSystem.MergeAudioPath`; also directly when a merge destroys both blocks (`SimEvent.DestroysBoth`) |
| `GameOver` | `event:/GameOver` | `BoardStepSystem`, when the move ends the game |

`MergeAudioPath(moverSize, resultSize)` is called with the moving block's size before the merge and the merge result's size. It returns Small if either is Small, else Medium if either is Medium, else Large. Because a merge result is never smaller than the input, the sound is effectively chosen by the moving block's original size: Small blocks merging gives the Small sound, Medium the Medium sound, Large the Large sound. A merge that clears both blocks always plays the Large sound (`Assets/Scripts/ECS/Systems/BoardStepSystem.cs`, `WriteBack`).

No other script or scene references FMOD events: there are no `StudioEventEmitter` components, no music and no UI sounds (search of `Assets/Scripts` and scenes; the only FMOD components in the two scenes are `StudioListener`).

### FMOD for Unity settings
`Assets/Resources/FMOD/FMODStudioSettings.asset` (the main `FMODUnity.Settings` object):

| Setting | Value | Meaning |
|---|---|---|
| `HasSourceProject` | 1 | A Studio project is linked |
| `sourceProjectPath` | `../Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro` | Relative to the Unity project folder |
| `sourceBankPath` | `../Cathartic Colours - FMOD/Build` | Bank source folder; per-platform subfolder is appended (see below) |
| `ImportType` | 0 (`StreamingAssets`) | Banks are loaded from streaming assets in builds; the other option is AssetBundle (`Packages/com.fmod.fmod-for-unity.v204/src/Settings.cs`) |
| `TargetAssetPath` | `FMODBanks` | Only used for the AssetBundle import type |
| `BankLoadType` | 0 (`All`) | Load the master banks and all banks listed in `Banks` at start-up |
| `MasterBanks` | `Master` | Loads `Master.strings` then `Master` (`RuntimeManager.BanksToLoad`) |
| `Banks`, `BanksToLoad` | empty | No other banks |
| `AutomaticEventLoading` | 1 | Legacy settings flag; start-up loading is driven by `BankLoadType` (meaning of this flag not confirmed in the plugin source) |
| `AutomaticSampleLoading` | 0 | Bank sample data is not pre-loaded at start-up (`RuntimeManager.cs`) |
| `LoggingLevel` | 1 | |
| `LiveUpdatePort` | 9264 | (per-platform LiveUpdate values are all "not overridden") |
| `AndroidUseOBB` / `AndroidPatchBuild` | 0 / 0 | |
| `Platforms` | 12 entries (Default, Play In Editor, Windows, Mac, Linux, Android, iOS, tvOS, visionOS, WebGL, Mobile Low, Mobile High) | Only `Default` and `PlayInEditor` have `active: 1`; no platform overrides a `BuildDirectory` (every `BuildDirectory` has `HasValue: 0`) |

Only start-up bank loading for `ImportType.StreamingAssets` is done by `RuntimeManager.LoadBanks` (`Packages/com.fmod.fmod-for-unity.v204/src/RuntimeManager.cs`). In the Editor the plugin reads banks straight from the source bank path; in a player build the Unity build step copies them to StreamingAssets (standard FMOD for Unity behaviour; not exercised in this repo). `Assets/StreamingAssets` does not exist on disk, and the Unity `.gitignore` ignores `.bank` files under `Assets/StreamingAssets/` (`Cathartic Colours/.gitignore`).

Platform build folders (checked 2026-11-19 in the package source; not run on a device):
- `Platform.cs` line 720 defaults a platform's `BuildDirectory` to `Desktop`. No platform in `FMODStudioSettings.asset` overrides it (every `BuildDirectory` has `HasValue: 0`), so the editor and Windows builds read `Build/Desktop/`.
- `PlatformMobileLow.cs` lines 21-25 declare the Android and iOS runtime platforms as the Mobile platform template (`PlatformMobileHigh` inherits it, `PlatformMobileHigh.cs` line 13). Neither sets a build directory, and both are inactive in the settings asset (`active: 0`).
- So nothing in the package or the committed settings points Android at `Build/Mobile/`. Whether an Android build picks up the Mobile banks or falls back to `Desktop` was **not verified**: [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10) (Android builds) has to check it and, if needed, override the `BuildDirectory` for Android in the FMOD settings.

The Android native libraries are present under `Packages/com.fmod.fmod-for-unity.v204/platforms/android/lib/`.

### Local changes to the FMOD package
The package folder is vendor code ([ADR-003](../decisions/ADR-003-fmod-embedded-package.md)). These are the only local changes to it, so the next FMOD update can redo or drop them:
- **`.meta` files re-saved in the Unity 6.6 format (CC-033, 2026-11-19).** 56 files that FMOD 2.04 ships in an older format, below Unity 6.6's minimum, so the editor warned "Open and re-save the file to upgrade" on every open:
  - 29 native plugin metas under `platforms/` (PluginImporter version 1 → 3)
  - 27 editor icon metas under `images/` (TextureImporter version 2 or 9 → 10)

  They were re-saved with `AssetDatabase.ForceReserializeAssets(…, ReserializeMetadata)` from a throwaway editor script. Each plugin keeps the platforms it's enabled for. The only change there is that `libresonanceaudio.so` drops `LinuxUniversal`, a platform Unity 6.6 no longer has. After an FMOD update, check the first editor open for this warning and repeat the re-save if it appears.

### FMOD Studio project layout
`Cathartic Colours - FMOD/` is saved in FMOD Studio 2.04 (checked 2026-11-19). Tracked paths:

| Path | Contents |
|---|---|
| `Cathartic Colours - FMOD.fspro` | Project stub: an empty `<objects serializationModel="Studio.02.04.00" />` |
| `Metadata/` | The saved project: one XML file per object. Subfolders (file counts): `Asset` 1, `AudioFile` 120, `Bank` 1 (`Master`), `BankFolder` 1, `CurvePresetFolder` 1, `DataFile` 2, `EffectPresetFolder` 1, `EncodingSetting` 2, `Event` 5, `EventFolder` 1, `ParameterPresetFolder` 1, `Platform` 2, `ProfilerFolder` 1, `Return` 1, `SandboxFolder` 1, `SnapshotGroup` 1 (the other subfolders are empty); plus `Master.xml`, `Mixer.xml`, `Tags.xml`, `Workspace.xml` |
| `Metadata/Event/` | The five game events, matching the `FMODEventPaths` constants |
| `Assets/` | 122 source audio files |
| `Build/Desktop/` | `Master.bank` (88,512 bytes), `Master.strings.bank` (972 bytes) |
| `Build/Mobile/` | `Master.bank` (174,912 bytes), `Master.strings.bank` (972 bytes) |
| `.gitignore` | Ignores the three folders below |

Ignored (per-machine, not in git since CC-003):

| Path | Contents |
|---|---|
| `.cache/` | Build cache and build records; FMOD uses it to skip banks that are up to date |
| `.user/` | Per-user editor layout |
| `.unsaved/` | FMOD Studio's store of unsaved edits. FMOD Studio 2.04 also writes per-user layout copies here when it closes |

Of the metadata files, 12 carry `Studio.02.04.00` and 134 still carry `Studio.02.03.00`. That is normal: FMOD rewrites a file only when it changes. Do not edit them by hand.

### Platforms and encodings
The project has two platforms, each with its own bank subfolder and encoding:

| Platform | Id | Subdirectory | Encoding |
|---|---|---|---|
| Desktop | `{37b3f97d-8431-4d11-921a-8c684c5c3073}` | `Desktop` | Vorbis, quality 37 (`encodingFormat` 3) |
| Mobile | `{f18948da-0041-47ce-a9e1-bfa9fb3c5bb5}` | `Mobile` | FADPCM, 24 kHz (`encodingFormat` 1, `sampleRate` 24000) |

The encodings were kept as Ronan set them (decision D3 of CC-003). Revisit them in CC-017 (performance). Which of the two folders Unity uses on each platform: see "Platform build folders" above.

### Rebuilding the banks
The banks in `Build/` are committed so Unity builds and plays from a clean checkout without FMOD Studio. After any change in FMOD Studio:
1. Save the project in FMOD Studio (Ctrl+S), then close FMOD Studio. Unsaved changes are not built.
2. Run the script from the repo root:
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-fmod-banks.ps1`
3. Commit the changed files in `Build/` together with the changed files in `Metadata/`.

Options: `-Platforms Desktop,Mobile` (default: both), `-Diagnostic` (validate the project, then build), `-FmodStudioCl <path>`, `-LogDir <dir>`. The script wraps `fmodstudiocl.exe -build` from FMOD Studio 2.04.00 (default path `C:\Program Files\FMOD SoundSystem\FMOD Studio 2.04.00\fmodstudiocl.exe`), writes logs to `%TEMP%\fmod-bank-build-<timestamp>`, lists the banks written and returns the tool's exit code. If the tool is missing it prints a message and exits 1.

Notes:
- FMOD skips banks whose build records in `.cache/` are current, so a run on an unchanged project may write nothing.
- A build from a fresh checkout (no `.cache/`) wrote banks byte-identical to the committed ones (checked 2026-11-19), and `-Diagnostic` reported no errors or warnings.
- Never run it against a project that is open in FMOD Studio. Agents run it only in the sandbox copy.

## Interactions with other systems
- Event entities are created inside the simulation: [ecs-frame-order.md](ecs-frame-order.md) (event entity table, unordered `AudioSystem`) and [merge-rules.md](merge-rules.md) (which merges happen).
- No presentation code plays sound; there is no audio setting in the menu or in `GameConfiguration` ([configuration.md](configuration.md)).
- Android builds, and checking that they use the Mobile banks, are future work: [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10).

## Known issues
- Android: no Android build or device test has been done yet, and which bank folder an Android build reads is not verified (see "Platform build folders"). [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10)
- `AudioSystem` is unordered against the systems that create events, so playback timing is not deterministic. [ecs-frame-order.md](ecs-frame-order.md)
- Note (design, confirmed by Ronan 2026-11-18): the merge sound is selected from the moving block's size, i.e. the *input* size, not the size produced (e.g. Medium + Medium plays `Block_Merge_Medium` although a Large block results). This is a naming choice, not an issue (`Assets/Scripts/ECS/Systems/BoardStepSystem.cs`, `MergeAudioPath`).

## Related test cases
- [Audio](../test-plan.md#audio-audio): TP-AUDIO-01 to TP-AUDIO-06; [Editor and tooling](../test-plan.md#editor-and-tooling-tool): TP-TOOL-04.
