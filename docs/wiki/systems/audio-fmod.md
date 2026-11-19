# Audio and FMOD

## Purpose
Describes how the game plays sound: simulation systems create audio event entities carrying an FMOD event path, `AudioSystem` plays them as one-shots, and FMOD for Unity loads banks that were built from the separate FMOD Studio project. Also records what the FMOD Studio project and the built banks contain today, including the hazard that the game's five events are not saved in the project proper.

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
| `Cathartic Colours - FMOD/Build/Desktop/Master.bank`, `Master.strings.bank` | Built banks that Unity reads |

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

Platform build folders: the only Studio platform is `Desktop`, and `Packages/com.fmod.fmod-for-unity.v204/src/Platform.cs` (line 720) defaults a `BuildDirectory` to "Desktop" for a desktop-type platform (which Unity platform classes use it was not checked). The Studio project has only a `Desktop` platform, so there is **no mobile build output**: see Known issues, [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10). The Android native libraries are present under `Packages/com.fmod.fmod-for-unity.v204/platforms/android/lib/`.

### Local changes to the FMOD package
The package folder is vendor code ([ADR-003](../decisions/ADR-003-fmod-embedded-package.md)). These are the only local changes to it, so the next FMOD update can redo or drop them:
- **`.meta` files re-saved in the Unity 6.6 format (CC-033, 2026-11-19).** 56 files that FMOD 2.04 ships in an older format, below Unity 6.6's minimum, so the editor warned "Open and re-save the file to upgrade" on every open:
  - 29 native plugin metas under `platforms/` (PluginImporter version 1 → 3)
  - 27 editor icon metas under `images/` (TextureImporter version 2 or 9 → 10)

  They were re-saved with `AssetDatabase.ForceReserializeAssets(…, ReserializeMetadata)` from a throwaway editor script. Each plugin keeps the platforms it's enabled for. The only change there is that `libresonanceaudio.so` drops `LinuxUniversal`, a platform Unity 6.6 no longer has. After an FMOD update, check the first editor open for this warning and repeat the re-save if it appears.

### FMOD Studio project layout
`Cathartic Colours - FMOD/` (FMOD Studio 2.03, `serializationModel="Studio.02.03.00"`):

| Path | Contents (what was found) |
|---|---|
| `Cathartic Colours - FMOD.fspro` | 90-byte project stub: an empty `<objects serializationModel="Studio.02.03.00" />` |
| `Metadata/` | The saved project. Subfolders `Asset`, `AudioFile` (120 files), `Bank` (1: `Master`), `BankFolder` (1), `DataFile` (2), `EffectPresetFolder`, `EncodingSetting`, `EventFolder` (1: the root "Master" event folder), `ParameterPresetFolder`, `Platform` (1: "Desktop", subdirectory `Desktop`), `ProfilerFolder`, `Return`, `SandboxFolder`, `SnapshotGroup`; plus `Master.xml` (master bus), `Mixer.xml`, `Tags.xml`, `Workspace.xml` |
| `Metadata/Event/` | **Does not exist.** `Metadata/` has `EventFolder/` (singular root folder object only) but no `Event` folder, so the saved project contains **no events** |
| `Assets/` | 122 files: source audio (`.wav` files, `READ_ME.rtf`, `.DS_Store`) |
| `Build/Desktop/` | `Master.bank` (88,384 bytes) and `Master.strings.bank` (960 bytes). `Desktop` is the only platform folder; there is no Android or iOS folder |
| `.cache/` | FMOD Studio build cache (`.pdc` files, `buildrecords/Desktop/Master.br`, `fsbcache/Desktop/*.fobj`) |
| `.user/` | Per-user state (`Metadata/Asset`, `Mixer.user.xml`, `Workspace.user.xml`) |
| `.unsaved/` | Unsaved changes, see below |

### The `.unsaved/` hazard
> **Resolved 2026-11-19:** Ronan saved the FMOD project, so the five events are now in `Metadata/Event/`, and Mobile banks exist in `Build/Mobile/`. The tables in this section describe the earlier state. Old copies still tracked in `.unsaved/`, the 2.03 to 2.04 Studio migration and the rewrite of this section belong to [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9).

`Cathartic Colours - FMOD/.unsaved/` holds changes FMOD Studio keeps outside the project until the user saves:

| Path | Contents |
|---|---|
| `.unsaved/Metadata/Event/` | **Five** event files: `Block_Merge_Medium`, `Block_Merge_Small`, `Block_Merge_Large`, `Block_Move`, `GameOver` (event `name` values; each is attached to the `Master` bank through its `banks` relationship and to the root `Master` event folder; each has a single track named "Audio 1" with a sound module; `Block_Move` references two audio files) |
| `.unsaved/Metadata/AudioFile/` | 242 audio-file entries (the saved `Metadata/AudioFile/` has 120) |
| `.unsaved/Metadata/DataFile/` | 2 entries (`READ_ME.rtf`, `.DS_Store`) |
| `.unsaved/.user/Metadata/` | Per-user state (asset, mixer and workspace) |
| `.unsaved/metadataFileMapping.data` | FMOD Studio's mapping of unsaved files |

The five event names equal the five `FMODEventPaths` constants. Until FMOD Studio saves the project (Ctrl+S), the events exist only in `.unsaved/`; rebuilding banks from the saved project alone would produce banks with no events, and the next clean checkout would not contain them. This is the CC-003 hazard: [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9). Project instructions say to restore and save them before any audio work (`CLAUDE.md` section 5).

The built banks in `Build/Desktop/` nevertheless appear to contain the events: the prefix-compressed string table in `Master.strings.bank` includes the tokens `event:/`, `Block_M`, `erge_`, `Large`, `Medium`, `Small`, `ove` and `GameOver`, and `Master.bank` is 88 KB. This was inferred from raw text in the bank files, not from FMOD tooling, so treat it as unverified. If it holds, sound works today because Unity reads the already-built banks, while the Studio project itself cannot reproduce them.

## Interactions with other systems
- Event entities are created inside the simulation: [ecs-frame-order.md](ecs-frame-order.md) (event entity table, unordered `AudioSystem`) and [merge-rules.md](merge-rules.md) (which merges happen).
- No presentation code plays sound; there is no audio setting in the menu or in `GameConfiguration` ([configuration.md](configuration.md)).
- Android build and FMOD mobile platform setup are future work: [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10).

## Known issues
- The five FMOD events exist only in `.unsaved/`; the saved project has none. [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9)
- No Android/mobile bank build or FMOD mobile platform configuration: only `Build/Desktop/` exists and no platform sets a `BuildDirectory`. [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10)
- `AudioSystem` is unordered against the systems that create events, so playback timing is not deterministic. [ecs-frame-order.md](ecs-frame-order.md)
- There is no `Metadata/Event/` folder and the `.fspro` is an empty stub, so a bank build from a fresh clone of the saved project would contain no events (`Cathartic Colours - FMOD/Metadata/`). Part of [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9).
- Note (design, confirmed by Ronan 2026-11-18): the merge sound is selected from the moving block's size, i.e. the *input* size, not the size produced (e.g. Medium + Medium plays `Block_Merge_Medium` although a Large block results). This is a naming choice, not an issue (`Assets/Scripts/ECS/Systems/BoardStepSystem.cs`, `MergeAudioPath`).

## Related test cases
- [Audio](../test-plan.md#audio-audio): TP-AUDIO-01 to TP-AUDIO-05.
