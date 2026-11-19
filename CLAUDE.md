# CLAUDE.md — Cathartic Colours

Project-specific rules and wiki schema. The **general workflow** (planning, sessions, git, wiki model, roadmap fields, test plans, quiz, model routing) lives in Ronan's user-level `~/.claude/CLAUDE.md`. Read both before doing anything. If the user-level file isn't loaded in this session, say so and ask Ronan before starting work.

Changes to this file are logged in `docs/wiki/log.md` (op `schema`).

---

## 1. Project at a glance
- **Game:** Cathartic Colours — casual 2048-style colour-merge puzzle game. Target: **Android release**.
- **Repo:** `github.com/rtumelty/Cathartic-Colours`. Branches per user-level §4 (`master`, `develop`, `feature/CC-<nnn>-<slug>`).
- **Layout**
  - `Cathartic Colours/` — Unity project (Unity **6000.6.9f1**, URP 2D 17.6.0, Entities 6.6.0 / DOTS ECS, Input System, UI Toolkit, Unity IAP 5.5.1, FMOD for Unity **2.04** as an embedded package in `Packages/com.fmod.fmod-for-unity.v204`, see [ADR-003](docs/wiki/decisions/ADR-003-fmod-embedded-package.md))
  - `Cathartic Colours - FMOD/` — FMOD Studio 2.03 project (Unity reads banks from its `Build/` folder; not yet migrated to 2.04)
  - `CatharticColours.pz` — original PuzzleScript prototype (reference only)
  - `docs/wiki/` — LLM-maintained wiki · `docs/raw/` — immutable sources
- **Roadmap id prefix:** `CC` (e.g. `CC-001`, deliverable `CC-001.2`).
- **Asset serialization:** Force Text. Always commit `.meta` files with their assets.

## 2. Wiki specifics
- Paths as in the user-level schema: `docs/wiki/{index,log,roadmap,test-plan}.md`, `features/`, `decisions/`, `systems/`.
- Test plan areas (initial): Main menu · Standard mode · Color Merge mode · Advanced Color Merge mode · Gameplay HUD · Game over · Save/Resume · Audio · Project tracking (GitHub, since CC-034). Add areas as features land.
- Systems pages to maintain (`docs/wiki/systems/`): `ecs-frame-order`, `merge-rules` (per mode, plus scoring, spawning and end conditions), `configuration`, `save-system`, `audio-fmod`, `input`, `ui-layout`, `presentation`.

## 3. Codebase patterns to follow
- **Namespaces:** `Simulation`, `ECS.Components`, `ECS.Systems`, `ECS.Utilities`, `Gameplay`, `Data`, `SaveSystem`.
- **Rules run in the pure simulation; ECS wraps it** ([ADR-001](docs/wiki/decisions/ADR-001-pure-board-simulation.md), since CC-006). The `CatharticColours.Simulation` assembly (`Assets/Scripts/Simulation/`, namespace `Simulation`) references only `Unity.Mathematics` and `Unity.Collections`: no `UnityEngine.Object`, Entities or `GameManager`. It holds the block enums (`BlockColor`, `BlockSize`, `ScoreTier`, `GameMode`) and, as CC-006 lands, the board state, merge rules and move step. New rule logic goes here, with EditMode tests in `Assets/Tests/EditMode/` (`CatharticColours.Simulation.Tests`).
- **ECS:** `partial struct` systems implementing `ISystem`, ordered with `[UpdateInGroup]`/`[UpdateAfter]`/`[UpdateBefore]`; singleton components for game state (`GameStateComponent`, `ScoreComponent`, `GridConfigComponent`, `GameModeComponent`); **tag components** to enable systems (`SpawnColorSystemTag`, mode tags); **event entities** for one-off effects (`AudioEventComponent`, `MergeEventComponent`) consumed and destroyed by their system; `EntityCommandBuffer` for structural changes.
- **Rules as strategies:** merge rules implement `IMergeUtility`, chosen by `MergeUtilityFactory` from `GameMode`. (A managed interface, so `BlockMovementSystem` cannot be Burst-compiled; new rule code should take a Burst-compatible form, see below.)
- **Favour Burst-compatible solutions** in ECS code: unmanaged components and data, no managed objects, interfaces or statics (`GameManager`) inside systems, `[BurstCompile]` on systems where possible. A non-Burst choice needs a stated reason on the feature page.
- **Configuration as ScriptableObjects:** `GameConfiguration`, `ColorProfile` (menu `CatharticColors/…`); runtime copies held by static `GameManager`.
- **Presentation** (MonoBehaviours: `GameRenderer`, `GameUIManager`, `JellyManager`, `PointsParticleSpawner`) reads ECS state; UI is UI Toolkit (UXML/USS), elements found by name with `root.Q<>()`.
- **Audio:** FMOD one-shots by event path constants in `FMODEventPaths`.
- **Save:** JSON via `SaveGameManager` (`persistentDataPath/savegame.json`).

## 4. Running tests and builds
- **Tests (command line):**
  `"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" -batchmode -projectPath "Cathartic Colours" -runTests -testPlatform EditMode -testResults "TestResults\editmode.xml" -logFile -`
  then the same with `-testPlatform PlayMode`. (Do not pass `-quit` with `-runTests`.) Close the editor first: a batchmode run can't open a project that's already open. The path above is the Unity Hub default install location for 6000.6.9f1. EditMode runs the `CatharticColours.Simulation.Tests` assembly (since CC-006); PlayMode has no tests yet and reports 0, which is expected.
- **Known constraint:** apart from the `Simulation` assembly, game scripts compile into `Assembly-CSharp`, which test assemblies can't reference. ECS, presentation and save code can't be unit-tested until the rest of CC-002 adds their assembly definitions.
- **Android build:** no Android build profile or build script is committed yet. Until one exists, builds are a `MANUAL (Ronan)` step.

## 5. Editor-only and external-tool work
- Scene/prefab wiring, UXML layout changes with complex bindings and all FMOD Studio edits are `MANUAL (Ronan)` unless trivially safe.
- **FMOD:** always **save the FMOD project** (Ctrl+S) before building banks or committing. Unsaved changes live in `.unsaved/` and are not part of the project. The Studio project is still on 2.03 while the Unity integration is on 2.04: migrate the Studio project to 2.04 (open it in FMOD Studio 2.04 and save) before rebuilding banks, so the banks match the runtime.

## 6. Sessions and environment
- **Planning session:** one long-running session owns the roadmap, refinement and feature plans (user-level §2.1). On approval it sets the issue to Approved and creates the deliverable sub-issues with `gh`. It then creates `feature/CC-<nnn>-<slug>` from `develop`, commits the feature page (with its index/log entries and any ADRs) there and pushes it. It commits to `develop` only when `roadmap.md` strategy changes. The kickoff prompt names the branch, the feature page and the issue number.
- **Tracking (since CC-034, [ADR-002](docs/wiki/decisions/ADR-002-issue-tracking.md)):**
  - Board: **user project #1**, <https://github.com/users/rtumelty/projects/1> (node id `PVT_kwHOAGm39c4BsNZC`), linked to the repo.
  - Fields: `Status`, `Severity`, `Size`, `Phase` (Foundations, Core refactor, Quick fixes, Gameplay, Platform and monetisation, Polish).
  - Labels: `type:feature|bug|chore`, `severity:critical|high|medium|low`, `size:S|M|L|XL`.
  - Views: Board, Backlog by severity, Bugs. Workflows set Done when an item is closed or its PR is merged.
  - `roadmap.md` is only the strategy page plus an id-to-issue table. Wiki links to an item point at its issue URL.
- **New finds:** create the issue, then put it on the board. Next id = highest `CC-` issue title + 1. Then add a row to the `roadmap.md` id-to-issue table in the next commit made for another reason.
  ```
  gh issue list --repo rtumelty/Cathartic-Colours --search "CC- in:title" --state all --limit 500 --json title --jq '.[].title' | sort | tail -1
  gh issue create --repo rtumelty/Cathartic-Colours --title "CC-nnn: <title>" --label type:bug,severity:medium,size:S --body "<description, wiki links, Depends on: #n>"
  gh project item-add 1 --owner rtumelty --url <issue url>
  gh project item-edit --project-id PVT_kwHOAGm39c4BsNZC --id <item id> --field-id <field id> --single-select-option-id <option id>
  ```
  Field and option ids: `gh project field-list 1 --owner rtumelty --format json`.
- **Status changes** are `gh project item-edit` calls (Status field), never commits. Close an issue with `gh issue close <n> --reason completed` when it's Done.
- **`gh` on Windows:** `C:\Program Files\GitHub CLI\gh.exe`, signed in as rtumelty with the `project` scope. Refresh `PATH` as for git (below). In PowerShell 5.1, pass GraphQL through `gh api graphql --input <file>` or Git Bash, not inline `-f query=...`: PowerShell mangles the quotes.
- **Feature sessions run locally only**, on Ronan's machine in this folder, with Remote Control enabled so he can follow them from other devices. Don't use or suggest cloud sessions: they can't see the user-level `~/.claude/CLAUDE.md` and have no Unity editor, FMOD Studio or Android SDK. Still push the planning commits and the feature branch before a session starts, so `origin` stays the source of truth.
- **Local git (Windows):** Git for Windows at `C:\Program Files\Git\cmd`. Global `core.sshCommand` points at `C:/Windows/System32/OpenSSH/ssh.exe` so git uses the Windows ssh-agent key for the `git@github.com` remote. If `git` isn't found in an agent shell, refresh `PATH` from the Machine and User environment rather than asking Ronan.
