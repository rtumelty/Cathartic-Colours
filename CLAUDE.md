# CLAUDE.md — Cathartic Colours

Project-specific rules and wiki schema. The **general workflow** (planning, sessions, git, wiki model, roadmap fields, test plans, quiz, model routing) lives in Ronan's user-level `~/.claude/CLAUDE.md`. Read both before doing anything. If the user-level file isn't loaded in this session, say so and ask Ronan before starting work.

Changes to this file are logged in `docs/wiki/log.md` (op `schema`).

---

## 1. Project at a glance
- **Game:** Cathartic Colours — casual 2048-style colour-merge puzzle game. Target: **Android release**.
- **Repo:** `github.com/rtumelty/Cathartic-Colours`. Branches per user-level §4 (`master`, `develop`, `feature/CC-<nnn>-<slug>`).
- **Layout**
  - `Cathartic Colours/` — Unity project (Unity **6000.3.2f1**, URP 2D, Entities 1.4.4 / DOTS ECS, Input System, UI Toolkit, FMOD for Unity)
  - `Cathartic Colours - FMOD/` — FMOD Studio 2.03 project (Unity reads banks from its `Build/` folder)
  - `CatharticColours.pz` — original PuzzleScript prototype (reference only)
  - `docs/wiki/` — LLM-maintained wiki · `docs/raw/` — immutable sources
- **Roadmap id prefix:** `CC` (e.g. `CC-001`, deliverable `CC-001.2`).
- **Asset serialization:** Force Text. Always commit `.meta` files with their assets.

## 2. Wiki specifics
- Paths as in the user-level schema: `docs/wiki/{index,log,roadmap,test-plan}.md`, `features/`, `decisions/`, `systems/`.
- Test plan areas (initial): Main menu · Standard mode · Color Merge mode · Advanced Color Merge mode · Gameplay HUD · Game over · Save/Resume · Audio. Add areas as features land.
- Systems pages to maintain: merge rules (per mode), ECS frame order, save system, audio/FMOD, input, UI layout.

## 3. Codebase patterns to follow
- **Namespaces:** `ECS.Components`, `ECS.Systems`, `ECS.Utilities`, `Gameplay`, `Data`, `SaveSystem`.
- **ECS:** `partial struct` systems implementing `ISystem`, ordered with `[UpdateInGroup]`/`[UpdateAfter]`/`[UpdateBefore]`; singleton components for game state (`GameStateComponent`, `ScoreComponent`, `GridConfigComponent`, `GameModeComponent`); **tag components** to enable systems (`SpawnColorSystemTag`, mode tags); **event entities** for one-off effects (`AudioEventComponent`, `MergeEventComponent`) consumed and destroyed by their system; `EntityCommandBuffer` for structural changes.
- **Rules as strategies:** merge rules implement `IMergeUtility`, chosen by `MergeUtilityFactory` from `GameMode`.
- **Configuration as ScriptableObjects:** `GameConfiguration`, `ColorProfile` (menu `CatharticColors/…`); runtime copies held by static `GameManager`.
- **Presentation** (MonoBehaviours: `GameRenderer`, `GameUIManager`, `JellyManager`, `PointsParticleSpawner`) reads ECS state; UI is UI Toolkit (UXML/USS), elements found by name with `root.Q<>()`.
- **Audio:** FMOD one-shots by event path constants in `FMODEventPaths`.
- **Save:** JSON via `SaveGameManager` (`persistentDataPath/savegame.json`).

## 4. Running tests and builds
- **Tests (command line):**
  `"<UnityEditorPath>\Unity.exe" -batchmode -projectPath "Cathartic Colours" -runTests -testPlatform EditMode -testResults "TestResults\editmode.xml" -logFile -`
  then the same with `-testPlatform PlayMode`. (Do not pass `-quit` with `-runTests`.) Record the local Unity path in a gitignored file or here once known.
- **Known constraint:** game scripts currently compile into `Assembly-CSharp`, which test assemblies can't reference. Adding assembly definitions for game code is a prerequisite for automated tests.
- **Android build:** no Android build profile or build script is committed yet. Until one exists, builds are a `MANUAL (Ronan)` step.

## 5. Editor-only and external-tool work
- Scene/prefab wiring, UXML layout changes with complex bindings and all FMOD Studio edits are `MANUAL (Ronan)` unless trivially safe.
- **FMOD:** always **save the FMOD project** (Ctrl+S) before building banks or committing. Unsaved changes live in `.unsaved/` and are not part of the project. (As of Nov 2026 the game's five events exist only in `.unsaved/` — restore and save them before any audio work.)

## 6. Sessions and environment
- **Planning session:** one long-running session owns the roadmap, refinement and feature plans (user-level §2.1). On approval it commits and pushes the roadmap/index/log updates to `develop`, then creates `feature/CC-<nnn>-<slug>`, commits the feature page there and pushes it. The kickoff prompt names that branch.
- **Feature sessions** may run locally or in the cloud, so everything they need must be on `origin` before they start. A cloud session has no Unity editor, FMOD Studio or Android SDK: it can do docs and code that needs no editor, but not test runs, bank builds or device builds. Pick a local session for features that need those.
- **Local git (Windows):** Git for Windows at `C:\Program Files\Git\cmd`. Global `core.sshCommand` points at `C:/Windows/System32/OpenSSH/ssh.exe` so git uses the Windows ssh-agent key for the `git@github.com` remote. If `git` isn't found in an agent shell, refresh `PATH` from the Machine and User environment rather than asking Ronan.
