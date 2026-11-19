# CC-033 — Upgrade project to Unity 6000.6.9f1

| Field | Value |
|---|---|
| Issue | [#45](https://github.com/rtumelty/Cathartic-Colours/issues/45) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-033-unity-6-6-upgrade` (from `develop`, created by the planning session on approval) |
| PR | — |
| Depends on | [CC-034](https://github.com/rtumelty/Cathartic-Colours/issues/46) (done) |
| Blocks | [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12) (its command-line test runs use this editor) |

## Goal
The project opens, compiles and runs on Unity **6000.6.9f1**, the only editor on Ronan's machine. It uses only the packages it needs, on versions that editor supports, with FMOD for Unity 2.04 and Android Build Support installed. Command-line test runs work, and the editor path is recorded for later sessions.

## Scope
**In**
- Upgrade `ProjectSettings/ProjectVersion.txt` from 6000.3.2f1 to 6000.6.9f1 by opening the project in that editor (batchmode).
- **Strip unused packages and modules** from `Packages/manifest.json`, with usage evidence for each (D1).
- Resolve the kept packages to versions 6000.6 supports, mainly Entities 1.4.4 (Unity 6.4+ ships Entities, Collections and Mathematics as core packages), URP 17.3.0, Input System 1.17.0 and Test Framework 1.6.0.
- Install Android Build Support for 6000.6.9f1 through the Unity Hub CLI (D3). **No build is attempted.**
- FMOD for Unity moves from 2.03.11 to 2.04: Ronan imports it (MANUAL), and the agent commits it and fixes any compile fallout (D2).
- Fix compile errors and obsolete-API warnings in game code (`Assets/Scripts/`) caused by the upgrade.
- Run the EditMode and PlayMode `-runTests` commands on the new editor. An empty run is the smoke test of the test runner. Record the editor path in project `CLAUDE.md` §4 and the versions in §1.

**Out (they become backlog items if found)**
- Behaviour changes or bug fixes not caused by the upgrade. The known bugs (CC-020 to CC-032) stay as they are.
- Android build profile, build script and device builds ([CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10)).
- Migrating the FMOD Studio project to 2.04 and rebuilding banks. This needs the machine with the FMOD copy, and goes with [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9). The five missing events are ignored for now (Ronan, 2026-11-18).

## Approach
- **Batchmode first.** The agent runs the upgrade and every compile check with `Unity.exe -batchmode -quit -projectPath "Cathartic Colours" -logFile <file>`. Logs go in the scratchpad, not the repo. The editor GUI is needed only for Ronan's FMOD import and his play pass.
- **Strip before first open.** Remove the unused packages from `manifest.json` before the first 6.6 open, so pre-release packages (the `com.unity.ai.*` set) can't block resolution. Record evidence for each removal or keep (D1).
- **Engine changes before code fixes.** The generated changes go in their own commit: ProjectVersion, `manifest.json`, `packages-lock.json`, re-serialised settings and assets. The FMOD 2.04 import and the code fixes follow in later commits, so the re-serialisation noise can be reviewed apart from real changes (D4).
- **Entities is the main code risk.** The 6.6 ECS work aligns the `EntityId` type between GameObjects and Entities, and Entities moves to a core package. About 34 Entities API uses across 11 files (`Assets/Scripts/ECS/Systems/*`, `Gameplay/GameRenderer.cs`) could be affected. Fixes keep the existing patterns: `partial struct ISystem`, `SystemAPI`, ECB for structural changes. Don't rewrite systems that CC-006 replaces. Make the smallest change that compiles and behaves the same.
- **Re-serialisation review.** Before each commit, the reviewer reads the diff of changed `.asset`, `.unity` and `.prefab` files. It must contain only version and format bumps, or the removal of references to stripped packages. Any other content change is a divergence.

## Deliverables

### CC-033.1 — Strip packages, install Android support, upgrade editor
**Sub-issue:** [#51](https://github.com/rtumelty/Cathartic-Colours/issues/51) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary

1. Install Android Build Support for 6000.6.9f1 with the Unity Hub CLI (`"Unity Hub.exe" -- --headless install-modules --version 6000.6.9f1 -m android android-sdk-ndk-tools android-open-jdk --childModules`). Ronan approved the download on 2026-11-18. If it needs elevation, ask Ronan to run that one command.
2. Record current package versions (`manifest.json`, `packages-lock.json`).
3. Build the D1 evidence table: each package and module, used or unused, and how that was decided.
   - What counts as use: `using` directives and APIs in `Assets/Scripts/`; component types in scenes and prefabs; asset types (`.psd`, `.aseprite`, tilemaps, sprite shapes, timelines and so on); dependencies of kept packages; runtime needs (for example `androidjni` and `unitywebrequest` for FMOD on Android, and `jsonserialize` for `JsonUtility`).
   - Remove the unused ones from `manifest.json`.
4. Open the project in 6000.6.9f1 batchmode and keep the log. Update the kept packages per D1, and reopen until package resolution has no errors.
5. Commit the generated changes, settings changes and re-serialised assets, with their `.meta` files. Compile errors in FMOD 2.03.11 or in game code are listed for CC-033.2, not fixed here.

**Definition of Done**
- [ ] The Android module is installed (`Data/PlaybackEngines/AndroidPlayer` exists). No build was attempted.
- [ ] `ProjectVersion.txt` is 6000.6.9f1. Package resolution has no errors in the log.
- [ ] The D1 evidence table and the before/after package table are on this page.
- [ ] The re-serialisation diff has been reviewed.
- [ ] Leftover compile errors are listed for CC-033.2.
- [ ] Template items from user-level `CLAUDE.md` §3.

### CC-033.2 — FMOD 2.04 and compile/API fixes
**Sub-issue:** [#52](https://github.com/rtumelty/Cathartic-Colours/issues/52) · **Risk:** High · **Worker:** primary · **Review:** primary

Starts after MANUAL step 1 (Ronan imports FMOD for Unity 2.04).

1. Commit Ronan's FMOD import on its own: `Assets/Plugins/FMOD/**` with `.meta` files, and the FMOD settings asset.
   - Check that old 2.03 files the 2.04 package no longer ships are deleted, not left behind.
   - Check that `FMODEventPaths` still lines up with the integration API.
2. Fix every compile error and every new obsolete-API warning in `Assets/Scripts/`, keeping behaviour the same. FMOD plugin warnings are recorded, not fixed (third-party code), unless they are errors.
3. Run a batchmode compile that ends with zero `error CS` lines.

**Definition of Done**
- [ ] `fmod.cs` `VERSION.number` reports 2.04.x. There are no orphaned 2.03 files.
- [ ] A batchmode compile on 6000.6.9f1 has 0 errors and no new warnings in `Assets/Scripts/` (warning counts before and after recorded here).
- [ ] Each API change is listed on this page (old API → new API, file). Affected system pages are updated (`audio-fmod` for the integration version).
- [ ] No behaviour change beyond the API swaps. MANUAL step 2 is passed.
- [ ] Template items from §3.

### CC-033.3 — Test runner smoke run and docs
**Sub-issue:** [#53](https://github.com/rtumelty/Cathartic-Colours/issues/53) · **Risk:** Low · **Worker:** Haiku · **Review:** primary

1. Run the §4 EditMode and PlayMode `-runTests` commands on 6000.6.9f1. No test assemblies exist yet, so an empty run is expected. A clean exit and a results XML with 0 tests are the smoke test of the runner. Record the exit code and results paths. A failure here is a finding to fix or raise, not N/A.
2. Update project `CLAUDE.md`:
   - §1: 6000.6.9f1, the resolved Entities and URP versions, FMOD for Unity 2.04 (the Studio project stays on 2.03 until it's migrated).
   - §4: the editor path `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe`, which is a path, not a secret, so it's committed.
   - §5: note that the FMOD Studio project must be migrated to 2.04 before banks are rebuilt.
3. Update the wiki: affected system pages, index and log. Fold the feature test cases into `test-plan.md` under a new area, "Editor and tooling".

**Definition of Done**
- [ ] Both `-runTests` runs exit cleanly and write their results XML.
- [ ] `CLAUDE.md` §1, §4 and §5 are updated, with a log entry (op `schema`).
- [ ] Test plan updated.
- [ ] Template items from §3.

## Tests
No test assemblies exist yet. Game code is in `Assembly-CSharp`, which test assemblies can't reference ([CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8), [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)).

Verification:
- the batchmode compile (CC-033.2)
- the empty `-runTests` runs as the test-runner smoke test (CC-033.3)
- Ronan's editor regression pass (MANUAL step 2)

**Android build at completion (user-level §2.3): N/A** until CC-004 sets up build profiles. Ronan decided this on 2026-11-18.

## Risk and model routing

| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-033.1 | Medium | Sonnet | The primary reviews the strip evidence, package choices and re-serialisation diff |
| CC-033.2 | High | primary | FMOD 2.04 plus Entities/ECS API changes; judgement on behaviour parity |
| CC-033.3 | Low | Haiku | Commands and docs; primary review |

## MANUAL (Ronan)
1. **After CC-033.1 is approved: import FMOD for Unity 2.04.**
   - Download the 2.04 Unity integration from fmod.com (needs your account).
   - Open `Cathartic Colours` in Unity Hub with 6000.6.9f1.
   - Go to Assets → Import Package → Custom Package… → choose the `.unitypackage` → Import (all).
   - If the FMOD setup wizard or the "update" dialog opens, follow it, including deleting old integration files when it offers.
   - Close the editor and tell the session. Don't commit; the session commits the import.
2. **After CC-033.2: editor regression pass.**
   - Open the project in 6000.6.9f1 and check Window → General → Console: there should be no red errors after the domain reload.
   - Open the main scene and press Play. Run these test-plan areas in order: MENU, STD, CM, ACM, HUD, OVER, SAVE.
   - Then play one move with sound on. The runtime is now 2.04 and the banks were built with Studio 2.03. A newer runtime generally loads older banks, but confirm there's no bank-load error in the Console. Missing-event failures for the five unsaved events are expected (CC-003).
   - Report anything else that fails in the CC-033.2 review.

## Decisions
- **D1 — Packages:** strip every package and engine module with no evidence of use (Ronan, 2026-11-18).
  - **Kept regardless:** IDE integrations (Rider, Visual Studio). They are editor tooling, not project content.
  - **Removed even though they're useful later:** Profile Analyzer and Project Auditor. Both are dev tools with no project use. Re-add them in CC-017 if needed.
  - **Kept packages:** use the editor-resolved, verified or core versions. Don't move to a new major unless required.
  - **Re-adding:** a package later found to be needed is re-added and recorded as a divergence.
- **D2 — FMOD for Unity:** move to 2.04 by a manual import (Ronan). The FMOD Studio project migration is out of scope (CC-003).
- **D3 — Android Build Support:** the agent installs the Android module, SDK/NDK tools and OpenJDK for 6000.6.9f1 through the Unity Hub CLI. No build until CC-004 sets up build profiles (Ronan, 2026-11-18).
- **D4 — Commit shape:** three separate commits for reviewability:
  1. strip and upgrade (generated changes)
  2. the FMOD 2.04 import
  3. the code fixes

## CC-033.1 results

### Packages before and after
Manifest: 60 entries → 17. Lock: 31 resolved packages on 6000.6.9f1. Package resolution has no errors (third batchmode open: no package changes).

| Package | Before (6000.3.2f1) | After (6000.6.9f1) | Note |
|---|---|---|---|
| com.unity.entities | 1.4.4 | 6.6.0 | Core package in 6.6 (built-in version) |
| com.unity.render-pipelines.universal | 17.3.0 | 17.6.0 | Built-in version |
| com.unity.test-framework | 1.6.0 | 1.8.0 | Built-in version |
| com.unity.inputsystem | 1.17.0 | 1.20.1 | Bumped by the editor on resolve (minor) |
| com.unity.ide.rider | 3.0.38 | 3.1.0 | Bumped by the editor on resolve (minor) |
| Kept modules (12) | 1.0.0 | 1.0.0 | androidjni, animation, assetbundle, audio, imgui, jsonserialize, particlesystem, physics, ui, uielements, unityanalytics, unitywebrequest |
| Transitive, now in the lock | — | — | collections 6.6.0, burst 2.0.0, render-pipelines.core / shadergraph / universal-config 17.6.0, scriptablebuildpipeline 3.0.3, test-framework.performance 6.6.0, graph-authoring 1.0.0, modules.hierarchy / hierarchycore |

### D1 evidence
Evidence came from a scan of `Assets/` (scripts, scene/prefab classIDs and script GUIDs, asset types, asmdefs, FMOD sources) and `ProjectSettings/`, plus the 6000.6.9f1 built-in `package.json` dependency graph. Project contents: 44 scripts, 3 scenes, 4 prefabs, 4 `.anim` + 1 `.controller`, 1 `.inputactions`, UXML/USS/TSS, 1 ShaderGraph. There are no textures, sprites, `.psd`, `.aseprite`, tiles, sprite shapes, timelines, video or audio files.

**Kept**

| Package | Evidence |
|---|---|
| entities | 26 `using Unity.Entities;` in `Assets/Scripts` |
| render-pipelines.universal | Renderer2D/UniversalRP assets, `Light2D` and `UniversalAdditionalCameraData` in scenes, `JellyShader.shadergraph` |
| inputsystem | `GameInputHandler.cs` (`PlayerInput`, `InputActionAsset`), `Assets/Input/GameControls.inputactions` |
| test-framework | Plan: CC-033.3 `-runTests` smoke runs |
| ide.rider | Plan: IDE integration (Ronan uses Rider) |
| purchasing (5.5.1) | Kept: planned for CC-016 (one-time purchase that removes ads), decided by Ronan 2026-11-19. Added on `develop` and merged in; nothing uses it yet. Brings in services.core, services.deployment, ugui and newtonsoft-json as dependencies |
| modules.androidjni | FMOD `RuntimeManager.cs:194,199` `AndroidJavaClass` |
| modules.unitywebrequest | FMOD `RuntimeManager.cs:903` `UnityWebRequest.Get` (Android bank loading). Also an entities dependency |
| modules.jsonserialize | `JsonUtility` in `SaveGameSystem.cs` and `SaveGameManager.cs` |
| modules.particlesystem | `PointsParticleSpawner.cs`, `ScoreParticles.prefab` (classID 198/199) |
| modules.animation | `Block.prefab` Animator (95) with `Block.controller` and 4 clips |
| modules.physics | `Cell.prefab` BoxCollider (65). Also an entities and uielements dependency |
| modules.uielements | `UIDocument` in 3 scripts, UXML/USS/PanelSettings |
| modules.audio, assetbundle, unityanalytics | entities dependencies (6.6) |
| modules.imgui, ui | uielements dependencies (6.6) |

**Removed**

| Package | Evidence |
|---|---|
| feature.2d and its set: 2d.animation, 2d.aseprite, 2d.psdimporter, 2d.sprite, 2d.spriteshape, 2d.tilemap, 2d.tilemap.extras, 2d.tooling | No sprites, `.psd`, `.aseprite`, tiles or sprite shapes. No SpriteSkin, Tilemap (1839735485) or SpriteShapeController. Light2D and the 2D Renderer ship in URP |
| 2d.enhancers, ai.assistant, ai.generators, ai.inference | Plan rule: pre-release, no use |
| collab-proxy | Project uses git. `VersionControlSettings` is Visible Meta Files |
| multiplayer.center | Editor onboarding window. `MultiplayerManager` roles disabled |
| performance.profile-analyzer, project-auditor | Plan rule: dev tools (re-add in CC-017 if needed) |
| ide.visualstudio | Ronan uses Rider (Ronan, 2026-11-18) |
| timeline, modules.director | No PlayableDirector (320) or `.playable`. FMOD Timeline code is behind `#if UNITY_TIMELINE_EXIST` |
| visualscripting | No graphs or `Unity.VisualScripting` use. FMOD `BoltIntegration.cs` is behind `#if UNITY_VISUALSCRIPTING_EXIST` |
| ugui, modules.physics2d | No Canvas (223), `UnityEngine.UI`, 2D colliders or Physics2D calls. On 6.6, URP core no longer depends on ugui, so nothing kept pulls either in. FMOD guards on `UNITY_UI_EXIST` / `UNITY_PHYSICS2D_EXIST` |
| modules.terrain, terrainphysics | No terrain. On 6.6, URP core no longer depends on terrain |
| modules.ai, umbra | No NavMesh API or occlusion baking. Scenes hold only the default NavMeshSettings (196) and OcclusionCullingSettings (29), which load without the modules |
| modules.accessibility, adaptiveperformance, cloth, imageconversion, screencapture, tilemap, vectorgraphics, vehicles, video, vr, wind, xr, unitywebrequestassetbundle / audio / texture / www | No API, component or asset use. No kept package depends on them (URP gates XR and adaptive performance behind versionDefines) |
| modules.physicscore2d, tetgen, timelinefoundation | Added by the 6.6 upgrader to keep old monolithic-module behaviour. No kept package depends on them, and there's no 2D physics, light-probe tetrahedralization or Timeline. After removal the third open did not re-add them |

Settings cleaned with the strip:
- `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json` deleted.
- The `com.unity.dt.app-ui` entry removed from `EditorBuildSettings` `m_configObjects` (app-ui came via ai.inference).
- `ProjectAuditorSettings.asset` and `PhysicsCoreProjectSettings2D.asset` are re-created by the editor itself even with their packages and modules absent, so they are kept as editor-generated settings.
- `ProjectSettings/Packages/com.unity.dedicated-server/` is left as it is (unrelated, not stripped here).

### Re-serialisation review
- `ProjectVersion.txt` → 6000.6.9f1 (8c45277fb9c5).
- `UniversalRenderPipelineGlobalSettings.asset`: URP's own 17.6 migration (`m_AssetVersion` 9 → 11, renamed and added resource types, `m_EnableRenderGraph` and `m_EnableRenderCompatibilityMode` removed). Render Graph is now mandatory. There are no custom renderer features, but watch rendering in the MANUAL step 2 pass.
- `EditorBuildSettings.asset`: app-ui entry removed, `m_UseUCBPForAssetBundles` dropped (format).
- No scene, prefab or other asset changed.

### Left for CC-033.2
- `Assets\Plugins\FMOD\src\RuntimeManager.cs(666,45): error CS0619: 'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'` (FMOD 2.03.11). This is the only `error CS`. Game assemblies didn't compile past it, so game-code errors are still unknown.
- FMOD 2.03 warnings: UAC1002 (`Settings.cs` legacy settings lack `[Serializable]`) and UAC0009 (`DEVELOPMENT_BUILD` deprecated, in `fmod.cs`, `RuntimeManager.cs` and `Platform.cs`).
- Batchmode opens need `-ignorecompilererrors` until this is fixed. Without it the editor exits with code 1 before writing `ProjectVersion.txt`.

Android Build Support was installed by Ronan through the Hub (2026-11-18): `Data/PlaybackEngines/AndroidPlayer` with `SDK`, `NDK` and `OpenJDK`. No build was attempted.

## CC-033.2 results

### FMOD 2.04 import
- **Source:** `fmodstudio20400.tgz` from Ronan, a UPM tarball (`com.fmod.fmod-for-unity.v204` 2.4.0, `VERSION.number` 0x00020400). The agent installed it at Ronan's request; MANUAL step 1 is not needed.
- **Form:** embedded package at `Cathartic Colours/Packages/com.fmod.fmod-for-unity.v204/` (421 files) ([ADR-003](../decisions/ADR-003-fmod-embedded-package.md)). The lock records it as `embedded` and the manifest is unchanged.
- **Removed:** all of `Assets/Plugins/` (the 2.03.11 integration, 449 tracked files). No 2.03 files remain anywhere else (no `Assets/Gizmos/FMOD` or `Editor Default Resources/FMOD`).
- **Settings:** `FMODStudioSettings.asset` moved with its `.meta` to `Assets/Resources/FMOD/`, where 2.04 loads it. All 13 script GUIDs in it (Settings and 12 platform classes) match 2.04 scripts, so no sub-asset breaks. The scenes' `StudioListener` GUID (`86c6…`) also matches. All 27 non-FMOD script GUIDs in scenes, prefabs and assets resolve in the package cache.
- **Git rules:** the `.gitattributes` `eol=lf` rules and the `.gitignore` lib rule point at the package folder. The event cache rule is now `Assets/Resources/FMOD/FMODStudioCache.asset*`.
- **API:** the game's only FMOD call, `RuntimeManager.PlayOneShot(string, Vector3)` in `AudioSystem.cs`, is unchanged in 2.04, so `FMODEventPaths` still lines up.

### Compile on 6000.6.9f1
Batchmode open without `-ignorecompilererrors`: exit 0, **0 `error CS`**.

| Warnings | Before (2.03 on 6.6, first open) | After (2.04) |
|---|---|---|
| `Assets/Scripts` | unknown (game assemblies didn't compile past FMOD) | 1: CS0414 `JellyManager.wobbleActive` assigned but never used (`JellyManager.cs:26`). The code predates the upgrade, so the warning isn't upgrade-related and stays out of scope |
| FMOD package | CS0619 error; UAC1002 ×4 (`Settings.cs` legacy), UAC0009 ×4 | UAC0009 ×4 (`DEVELOPMENT_BUILD` deprecated: `fmod.cs:29`, `RuntimeManager.cs:313`, `Platform.cs:492,504`). Recorded, not fixed (vendor code) |

**API changes in game code: none.** Entities 1.4.4 → 6.6.0 needed no source changes, and there are no obsolete-API warnings in `Assets/Scripts`. The third D4 commit (code fixes) is therefore empty, and CC-033.2 is one commit.

FMOD may migrate `FMODStudioSettings.asset` (its `CurrentVersion` is still 2.03's 131857) the first time the editor GUI opens it. Any resulting change is committed after MANUAL step 2.

### Merge of develop (2026-11-19)
Ronan committed three changes to `develop` while CC-033 was in review: the FMOD Studio project saved out of `.unsaved/`, rebuilt Desktop banks plus new Mobile banks, and Android build profiles (`Assets/Settings/Build Profiles/`) with `com.unity.purchasing` 5.5.1. They were merged into this branch as follows:
- **FMOD Studio project, banks, build profiles:** taken as they are. The build profiles hold no signing settings. The Studio project is still serialised as 2.03 (`Studio.02.03.00`).
- **`FMODStudioSettings.asset`:** develop's edit, made by the 2.04 editor, carried over to the moved file in `Assets/Resources/FMOD/`. `CurrentVersion` is now 132096 (2.04), which is the migration expected after MANUAL step 2. A trailing `i` at the end of the `MigratedPlatforms` hex string was removed as a stray character. That was wrong: CC-033.3 showed that Unity 6.6 writes integer arrays with this `i` suffix itself (see CC-033.3 results). Unity reads both forms, and it puts the suffix back the next time it saves the file.
- **`com.unity.purchasing` 5.5.1:** kept by Ronan's decision (planned for CC-016) and added to the stripped manifest. Unity regenerated the lock rather than it being hand-merged. Purchasing brings back `com.unity.ugui` and the 2D physics modules as dependencies (not direct entries), plus Unity Services core and deployment and Newtonsoft JSON.
- **Build profiles:** a batchmode open did not re-save them. If the editor GUI re-saves them in 6.6 format, that's committed as a format-only change. No device build yet (Ronan, 2026-11-19).
- **Check:** batchmode open of a clean checkout of the merge (no `Library/`): exit 0, 0 `error CS`, same warnings as CC-033.2. Two first-import `LogError`s ("Host type is not matching any asset type") come from Unity's own render-pipelines.core package files.
- **Leftovers from the feature branch's working folder:** develop had also picked up `fmodstudioL.dll`, `Assets/Resources/FMOD.meta` and the FMOD event cache. The DLL and `.meta` are byte-identical to this branch's copies. The event cache was untracked again, because it's gitignored here (ADR-003).

### Outdated `.meta` formats (2026-11-19)
In the editor, Ronan saw many warnings that files were "below the supported minimum" format. A batchmode open listed 57 such `.meta` files:
- 56 in the FMOD package, as FMOD 2.04 ships them: 29 plugin metas under `platforms/` and 27 icon metas under `images/`.
- `Assets/UI/MainMenu.uxml.meta`, which had been saved without an importer block.

All 57 were re-saved in the 6.6 format by a throwaway editor script calling `AssetDatabase.ForceReserializeAssets` with `ReserializeMetadata`. The script was deleted and never committed. Other FMOD metas that the script also touched, but that weren't flagged, were reverted.

The plugin metas were then checked for which platforms each one is enabled for. They're unchanged, except that `libresonanceaudio.so` drops `LinuxUniversal`, which Unity 6.6 no longer has. A fresh batchmode open then reported 0 outdated files and 0 compile errors.

This is a change to vendor code that ADR-003 allows when a Unity upgrade forces it. It's recorded in [audio-fmod](../systems/audio-fmod.md#local-changes-to-the-fmod-package).

The editor also re-saved `ProjectSettings/PackageManagerSettings.asset` in the 6.6 registry format, committed as a format-only change. Unity IAP generated `Assets/Resources/BillingMode.json` (`{"androidStore":"GooglePlay"}`), its store setting for Android, committed separately.

## CC-033.3 results

### Test runner smoke run (2026-11-19)
With the editor closed, the two `-runTests` commands from project `CLAUDE.md` §4 were run on 6000.6.9f1. Results and logs went to the session scratchpad, not the repo.

| Run | Exit code | Results XML | Test cases | Log errors |
|---|---|---|---|---|
| EditMode | 0 | written, `result="Passed"` | 0 | none (`error CS`, exceptions, aborts) |
| PlayMode | 0 | written, `result="Passed"` | 0 | none |

The runner works on 6.6. 0 tests is the expected result until test assemblies exist (CC-002, CC-006).

### ProjectSettings re-saved in the 6.6 format
The batchmode runs re-saved `ProjectSettings/ProjectSettings.asset` (Player settings) from `serializedVersion` 28 to 30. The diff is format only:
- New 6.6 fields at their defaults, for example the iOS thermal-state frame rates, Android system-bar options and `enableDirectStorage`.
- Removed fields for features 6.6 no longer has: HoloLens tracking-loss settings, `AndroidAllowedArchitectures` and `vrEditorSettings`.
- `AndroidMinSdkVersion` raised from 25 to 26, the lowest Android version Unity 6.6 supports.
- Integer arrays stored as hex strings now end in `i`, for example `m_ColorGamuts: 00000000i`. This is how 6.6 writes them, so the suffix was kept.

It was committed with CC-033.3. Other files in the working tree that only changed line endings, or that Play mode changed (`BlockMaterial.mat` wobble values), were left out.

### Docs
- Project `CLAUDE.md`: §1 versions, §4 editor path and the close-the-editor note, §5 FMOD Studio 2.04 migration note. The old §5 note that the five events exist only in `.unsaved/` was replaced, since `develop` now has the saved Studio project.
- [test-plan](../test-plan.md): new area "Editor and tooling" (`TOOL`), cases TP-TOOL-01 to 03.

## Divergences
- **CC-033.3, routing (minor):** the Haiku worker ran the two test commands. The primary model wrote the docs, since the `CLAUDE.md` edits depended on findings from the whole feature. It also corrected its own CC-033 merge note about the `i` suffix.
- **CC-033, merge (minor):** `develop` gained three commits from Ronan during review (saved FMOD Studio project, Mobile banks, Android build profiles, Unity IAP) and was merged into this branch. Unity IAP stays by Ronan's decision (planned for CC-016), which brings ugui and the 2D physics modules back as its dependencies.
- **CC-033.2, D2 (minor):** FMOD 2.04 came as a UPM tarball, not a `.unitypackage`, so it's an embedded package under `Packages/`, not `Assets/Plugins/FMOD` (Ronan chose this, ADR-003). The agent did the import, not Ronan (Ronan, 2026-11-18). CC-033.2 started before CC-033.1 was approved, at Ronan's request.
- **CC-033.1, D3 tool (minor):** Unity Hub 4.0.1 (Store build) dropped `-- --headless install-modules` in favour of the standalone Unity CLI. The agent installed Unity CLI 1.0.1 with Ronan's OK (in `%LOCALAPPDATA%\Unity\bin`, agent-skills setup skipped). Module install with `--accept-eula` (also approved) then failed because UAC elevation was refused from the agent shell. Ronan installs the Android module through the Hub GUI instead (Ronan, 2026-11-18). The OpenJDK module id is `android-open-jdk-17.0.18+8`.
- **CC-033.1, D1 (minor):** the Visual Studio integration was removed rather than kept (Ronan prefers Rider). The worker's evidence kept ugui, physics2d and terrain as URP-core dependencies, based on the 6000.3 lock. The primary's review against the 6.6 built-in graph found no kept dependents, so they were removed.

## Feature test plan
1. Open the project in 6000.6.9f1. There is no upgrade prompt and no Console errors. → TP-TOOL-01
2. Run EditMode and PlayMode `-runTests` from the command line. Both exit cleanly and write a results XML (0 tests). → TP-TOOL-02
3. Package Manager → In Project lists only the packages kept in the D1 table. → TP-TOOL-03
4. Regression pass: MENU → STD → CM → ACM → HUD → OVER → SAVE behave as before the upgrade (known bugs unchanged). Audio plays with no bank-load error (missing events excepted).

## Status log
- 2026-11-18 Planned
- 2026-11-18 Revised: strip unused packages, FMOD 2.04 (manual import), Android module via Hub CLI with no build, empty test run as runner smoke test
- 2026-11-18 Approved by Ronan (plan, D1–D4, routing); sub-issues #51–#53 created
