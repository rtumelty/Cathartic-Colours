# CC-002 — Split game code into testable assemblies

| Field | Value |
|---|---|
| Issue | [#8](https://github.com/rtumelty/Cathartic-Colours/issues/8) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-002-game-assemblies` (from `develop`, created by the planning session on approval) |
| PR | [#78](https://github.com/rtumelty/Cathartic-Colours/pull/78) |
| Depends on | [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12) (Simulation assembly, done) |
| Blocks | [CC-041](https://github.com/rtumelty/Cathartic-Colours/issues/75) (quick-fix batch; its branch is cut after this merges) |
| Decision | [ADR-004](../decisions/ADR-004-core-presentation-assemblies.md) |

## Goal
All game code compiles into assemblies that tests can reference. ECS systems and the save path have automated EditMode tests, and a PlayMode smoke test plays a move in the real gameplay scene. Both test runs work from the command line.

## Scope
**In**
- `CatharticColours.Core` and `CatharticColours.Presentation` runtime assemblies as in ADR-004, with the `GameManager` split and the `MergeEffectsSystem` move.
- EditMode tests for `BoardStepSystem` in a test ECS world, and for the save and restore path.
- A PlayMode test assembly and smoke tests, run with the existing `-testPlatform PlayMode` command.
- A test-only override for the save file path, so tests never touch the player's real save.
- Docs: ADR-004, a new `systems/assemblies.md`, project `CLAUDE.md` §3 (where new code goes) and §4 (remove "game code can't be tested"; test counts), test-plan cases.

**Out (they become backlog items if found)**
- Behaviour changes and bug fixes. The quick-fix batch ([CC-041](https://github.com/rtumelty/Cathartic-Colours/issues/75)) handles the known bugs after this merges. A test that would fail because of a known bug is written for current behaviour and names the bug's issue, as CC-006 did.
- Renaming namespaces (for example `SwipeDetector`'s `Gameplay.CatharticColours`, or the global-namespace classes). They stay as they are, to keep serialised type references.
- Moving `Editor/Build` or the Simulation assemblies.

## Approach
- **Current code** (verified on `develop`, 2026-11-19):
  - `Data/` (2 files), `ECS/Components` (8), `ECS/Systems` (6), `ECS/Utilities` (1), `SaveSystem/` (2), `Gameplay/` (11) compile into `Assembly-CSharp`.
  - `GameManager` (global namespace, `Gameplay/GameManager.cs`) holds both the runtime config (`ActiveConfiguration`, `ActiveColorProfile`, `Initialize`, `ResetStatics`) and presentation references (`ActiveGameRenderer`, `ActiveGameUIManager`, `ActivePointsParticleSpawner`, `JellyManager`).
  - Core-side users of `GameManager`: `SaveGameSystem` (config and profile), `SaveGameManager` (`Initialize`). Presentation-side users: `MergeEffectsSystem` (spawner and renderer) and `Gameplay/` classes.
  - FMOD is used by `AudioSystem` (`FMODUnity`) and by name constants in `ECS/Utilities/FMODEventPaths.cs`.
- **Split:**
  - `GameManager` keeps its name and the config half, and moves to Core (folder `Assets/Scripts/Core/` or kept in place under an assembly definition reference; the worker picks whichever keeps the diff smallest and records it).
  - The four presentation properties move to a new static class in Presentation (suggested `PresentationRegistry`, global namespace like `GameManager`). Every caller is updated. `ResetStatics` behaviour is kept for both.
  - `MergeEffectsSystem` moves to Presentation; its namespace `ECS.Systems` and its ordering attributes stay.
  - Assembly references: Core → Simulation, Unity.Entities, Unity.Mathematics, Unity.Collections, Unity.Burst, FMODUnity. Presentation → Core, Simulation, Unity.Entities, Unity.Mathematics, Unity.Collections, Unity.InputSystem, FMODUnity if needed. `autoReferenced` stays true for runtime assemblies.
  - Keep `.meta` files with every moved file (`git mv`), so GUIDs don't change.
- **Tests follow the CC-006 pattern:** assembly per test kind under `Assets/Tests/`, NUnit, `UNITY_INCLUDE_TESTS` define constraint. ECS tests build a `World` with only the systems under test and its own singletons, and dispose it in `TearDown`. Burst compilation is synchronous in tests that check Burst-compiled systems.
- **Save path override:** an `internal` or test-only setter on `SaveGameManager` (with `InternalsVisibleTo` for the test assembly) that redirects reads and writes to a temp folder. The default path is unchanged.

## Deliverables

### CC-002.1 — Core and Presentation assemblies
**Sub-issue:** [#71](https://github.com/rtumelty/Cathartic-Colours/issues/71) · **Risk:** High · **Worker:** primary · **Review:** primary
1. Add the two assembly definitions, split `GameManager`, move `MergeEffectsSystem`, update callers.
2. Batchmode compile, then batchmode-open the main menu and gameplay scenes and the prefabs, and check the log for missing scripts or broken references.
3. Run the existing EditMode suite and one Android development build (`tools/build-android.ps1`).

**Definition of Done**
- [ ] 0 compile errors and no new warnings
- [ ] No `Assembly-CSharp` game code is left in `Assets/Scripts/` (only Unity-generated assemblies remain)
- [ ] No missing-script or broken-reference messages when the scenes and prefabs load; moved files keep their GUIDs (`.meta` unchanged)
- [ ] Core has no reference to Presentation (checked in the `.asmdef` files)
- [ ] Existing EditMode suite passes; the Android build succeeds
- [ ] Wiki log entry; sub-issue status set on the board
- [ ] Reviewed by primary; committed per user-level §4

### CC-002.2 — EditMode tests for the ECS board step
**Sub-issue:** [#72](https://github.com/rtumelty/Cathartic-Colours/issues/72) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
Tests in `CatharticColours.Core.Tests` on a test `World` running `BoardStepSystem` and `ClearMoveDirectionSystem`. They cover what CC-006.3 checked once with a temporary script:
- block entities keep their identity across a slide and a merge; merged-away blocks are destroyed; a new indicator gets a new entity;
- score and `CurrentFrameScore`; `MoveCount`; audio events and merge events (with points) created for the move;
- the resolved indicator and the new indicator; the cleared move command;
- a rejected move after game over creates no move sound and counts no move (CC-023);
- one case per game mode.

**Definition of Done**
- [ ] Every item above has at least one test; the full EditMode suite passes from the command line
- [ ] Tests dispose their worlds; no test depends on another's order
- [ ] Reviewed by primary; committed per user-level §4

### CC-002.3 — Save and restore tests
**Sub-issue:** [#73](https://github.com/rtumelty/Cathartic-Colours/issues/73) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. The test-only save path override (Approach).
2. Tests: a game state saved by `SaveGameSystem` and restored by `SaveGameManager` gives the same board, score, move count, mode and config values; a resumed game changes nothing until the first move (CC-022); game over deletes the save; `HasSaveGame` follows the file.
3. Known gaps (for example `colorProfileName` ignored on restore, CC-031) get a test of current behaviour that names the issue.

**Definition of Done**
- [ ] Tests never read or write the real `persistentDataPath/savegame.json` (checked by running them with a real save present and confirming it's unchanged)
- [ ] Full EditMode suite passes from the command line
- [ ] Reviewed by primary; committed per user-level §4

### CC-002.4 — PlayMode smoke tests and docs
**Sub-issue:** [#74](https://github.com/rtumelty/Cathartic-Colours/issues/74) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. `CatharticColours.PlayMode.Tests`: load the gameplay scene with a known configuration and the save path override, send one move through a `MoveDirectionComponent` entity, wait for the frame to finish, and check `MoveCount`, the score and that a block moved. Any logged error fails the test (`LogAssert`). One test starts from the main menu scene and enters a game.
2. Run EditMode and PlayMode from the command line (project `CLAUDE.md` §4 commands).
3. Docs: new `systems/assemblies.md` (assemblies, what goes where, references, test assemblies); project `CLAUDE.md` §3 (new code goes in Core unless it needs MonoBehaviours, UI, input or scenes) and §4 (remove the `Assembly-CSharp` constraint; PlayMode run is no longer empty); test plan area "Editor and tooling" case for both runs; wiki index and log.

**Definition of Done**
- [ ] PlayMode run passes from the command line with at least the two smoke tests
- [ ] Docs updated as listed; `index.md` has the new page
- [ ] Reviewed by primary; committed per user-level §4

## Tests
- **Unit (EditMode):** the board step in an ECS world (CC-002.2) and the save path (CC-002.3), in `CatharticColours.Core.Tests`.
- **System (PlayMode):** scene smoke tests (CC-002.4).
- **Existing:** the Simulation and build test suites keep passing after the split (CC-002.1).

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-002.1 | High | primary | Cross-cutting, hard to reverse; serialised references at stake |
| CC-002.2 | Medium | Sonnet | Tests against a clear list; primary checks the world setup |
| CC-002.3 | Medium | Sonnet | Small production change (path override) plus tests |
| CC-002.4 | Medium | Sonnet | PlayMode scene tests and docs |

## MANUAL (Ronan)
1. **After CC-002.1: editor check.** The agent asks first whether to switch the manual copy (`Cathartic-Colours_2`) to `feature/CC-002-game-assemblies`. Open the project, wait for the compile, open the main menu scene, press Play, start a game and make a few moves. Check the Console has no missing-script warnings and that sound, particles and jelly wobble still work.

## Decisions
- **D1 — Core and Presentation assemblies** ([ADR-004](../decisions/ADR-004-core-presentation-assemblies.md), Ronan, 2026-11-19).
- **D2 — Tests describe current behaviour.** Known bugs stay for CC-041; tests that depend on them name the issue.
- **D3 — Normal workflow.** One deliverable at a time with Ronan's approval (the no-stops exception applies only to CC-041).

## Divergences
Minor implementation specifics (no scope, architecture, order or routing change):
- **CC-002.1 folder layout.** The Core assembly definition lives in a new `Assets/Scripts/Core/` folder with `GameManager.cs`. `Data/`, `ECS/` and `SaveSystem/` join it through assembly definition references (`CatharticColours.Core.asmref`), so none of their files moved. The Presentation assembly definition sits in `Assets/Scripts/Gameplay/`. `MergeEffectsSystem.cs` moved to `Gameplay/` (namespace `ECS.Systems` unchanged). The presentation half of `GameManager` became `PresentationRegistry` (`Gameplay/PresentationRegistry.cs`, global namespace).
- **CC-002.1 system order.** `SaveGameSystem` (Core) had `[UpdateAfter(typeof(MergeEffectsSystem))]`, which Core can't compile once that system is in Presentation. The same constraint is now declared from the other side: `[UpdateBefore(typeof(SaveGameSystem))]` on `MergeEffectsSystem`. The resolved order in `PresentationSystemGroup` is unchanged (`MergeEffectsSystem` → `SaveGameSystem` → `EndFrameSystem`).
- **CC-002.1 reference check.** Scenes, prefabs and assets were checked with a temporary batchmode editor script (not committed): no missing scripts, and no broken references in scenes, prefabs or game configuration. It also found 7 broken references in URP render settings (`Settings/Rendering/Renderer2D.asset` probe-volume debug shaders, `Lit2DSceneTemplate.scenetemplate`), which this change doesn't touch and which predate it.
- **CC-002.4 test-plan folding.** The "Editor and tooling" area and TP-TOOL-02 (command-line test runs) already existed from CC-033, so TP-TOOL-02 was extended (EditMode and PlayMode contents, real save untouched) instead of adding a duplicate case. The feature test plan's editor and Android checks are folded in by adding CC-002 to the related ids of TP-TOOL-01 (no missing scripts), TP-DROID-01 and TP-DROID-04.
- **CC-002.4 main menu test.** The Start button is pressed with a real `NavigationSubmitEvent` sent to the UI Toolkit button (a synthetic `ClickEvent` is not handled by `Clickable`), so the real `StartGame` handler runs. The test sets a non-default mode and size on the `GameManager` runtime copy first (the menu keeps an already initialised `GameManager`) and checks the new game uses them.
- **CC-002.4 test configuration.** Tests load the Default configuration and colour profile assets through `AssetDatabase` (editor PlayMode only). Each test unloads the game scenes in `[UnityTearDown]` before it destroys the runtime configuration copies, since the scene objects read them every frame.
- **CC-002.4 stale doc line.** `systems/save-system.md` still said the save path was built twice (true before CC-002.3); corrected while linking the new assemblies page.

## Feature test plan
1. Command line: EditMode and PlayMode runs both pass.
2. Editor: open the project, no missing scripts; play a game in each mode with sound, particles and wobble.
3. Android: `tools/build-android.ps1` still produces a working APK.

## Status log
- 2026-11-19: Planned
- 2026-11-19: Approved by Ronan (Core + Presentation layout, four deliverables, routing)
