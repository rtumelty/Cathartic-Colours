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

## Divergences
—

## Feature test plan
1. Open the project in 6000.6.9f1. There is no upgrade prompt and no Console errors. → TP-TOOL-01
2. Run EditMode and PlayMode `-runTests` from the command line. Both exit cleanly and write a results XML (0 tests). → TP-TOOL-02
3. Package Manager → In Project lists only the packages kept in the D1 table. → TP-TOOL-03
4. Regression pass: MENU → STD → CM → ACM → HUD → OVER → SAVE behave as before the upgrade (known bugs unchanged). Audio plays with no bank-load error (missing events excepted).

## Status log
- 2026-11-18 Planned
- 2026-11-18 Revised: strip unused packages, FMOD 2.04 (manual import), Android module via Hub CLI with no build, empty test run as runner smoke test
- 2026-11-18 Approved by Ronan (plan, D1–D4, routing); sub-issues #51–#53 created
