# CC-006 — Extract pure board simulation from ECS

| Field | Value |
|---|---|
| Issue | [#12](https://github.com/rtumelty/Cathartic-Colours/issues/12) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-006-pure-simulation` (from `develop`, created by the planning session on approval) |
| PR | [#56](https://github.com/rtumelty/Cathartic-Colours/pull/56) |
| Depends on | [CC-034](https://github.com/rtumelty/Cathartic-Colours/issues/46) (GitHub Issues tracking) and [CC-033](https://github.com/rtumelty/Cathartic-Colours/issues/45) (Unity 6000.6.9f1 upgrade), both merged first |
| Absorbs | [CC-020](https://github.com/rtumelty/Cathartic-Colours/issues/32), [CC-021](https://github.com/rtumelty/Cathartic-Colours/issues/33), [CC-022](https://github.com/rtumelty/Cathartic-Colours/issues/34), [CC-023](https://github.com/rtumelty/Cathartic-Colours/issues/35), [CC-024](https://github.com/rtumelty/Cathartic-Colours/issues/36); part of [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8) |
| Decision | [ADR-001](../decisions/ADR-001-pure-board-simulation.md) |

## Goal
Every rule of a move (slide, merge, score, resolve, spawn, game over) runs in one pure, deterministic, unit-tested simulation, which ECS only wraps. Then fix the simulation bugs found in CC-001 against that tested core.

## Scope
- **In:** the `Simulation` assembly and its EditMode test assembly; moving rules and block enums into it; `BoardStepSystem` replacing the five move systems; removing presentation calls from simulation; score computed in the simulation; dead ECS code (CC-024); fixes for CC-020, CC-021, CC-022, CC-023; wiki and test plan updates.
- **Out:** new modes or rule types (CC-008), next-spawn preview (CC-007), save format changes such as versioning or atomic writes (CC-031), asmdefs for the remaining game code and PlayMode tests (rest of CC-002), the UI/restart bugs (CC-028, CC-029), any scene or prefab changes.

## Approach
Architecture per [ADR-001](../decisions/ADR-001-pure-board-simulation.md). Order of work:
1. Build the harness first, so every later step is tested.
2. Extract with **behaviour parity**. Characterisation tests are written from the wiki spec ([merge-rules](../systems/merge-rules.md), [ecs-frame-order](../systems/ecs-frame-order.md)), not from the new code, and pin today's behaviour, known bugs included.
3. Swap ECS onto the core with parity checked by the manual test plan. CC-023 and CC-024 go away here because the old seams no longer exist.
4. Fix the remaining bugs one at a time. Each fix first flips its characterisation test from "pins the bug" to "asserts the fix".

## Deliverables

### CC-006.1 — Simulation assembly and test harness
**Sub-issue:** [#13](https://github.com/rtumelty/Cathartic-Colours/issues/13) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary

- `Assets/Scripts/Simulation/CatharticColours.Simulation.asmdef` (references `Unity.Mathematics`, `Unity.Collections`; auto-referenced so `Assembly-CSharp` sees it)
- `Assets/Tests/EditMode/CatharticColours.Simulation.Tests.asmdef` (EditMode only, references the simulation assembly, NUnit/Test Framework)
- Move `BlockColor`, `BlockSize`, `ScoreTier` and `GameMode` into `Simulation` (namespace `Simulation`); update `using`s across the game code. Enum values stay the same, so saves and assets are unaffected.
- One passing smoke test; the EditMode command-line run from project `CLAUDE.md` §4 works (the Unity path is recorded there by CC-033)
- Project `CLAUDE.md` §3: add the `Simulation` namespace and the ADR-001 pattern (log op `schema`)

**DoD (beyond template)**
- [ ] Project compiles in Unity 6000.6.9f1 with no new warnings; all `.meta` files committed
- [ ] `-runTests -testPlatform EditMode` passes from the command line, result XML checked
- [ ] `GameConfiguration` and `ColorProfile` assets still load with their values (checked by opening the assets in batchmode, or by test)

### CC-006.2 — Pure board model and rules (parity)
**Sub-issue:** [#14](https://github.com/rtumelty/Cathartic-Colours/issues/14) · **Risk:** High · **Worker:** primary implements the core; Sonnet writes the characterisation tests from the wiki spec without looking at the new implementation · **Review:** primary

- `Block` (colour, size, indicator flag), `BoardState` (width, height, `NativeArray<Block>` cells, `IDisposable`), `SimEvent` (moved, merged with position/tier/colour, indicator resolved, indicator spawned, game over), `StepResult` (accepted, score delta, game over)
- The three rule sets move into `Simulation` as structs over `Block`: `StandardMergeRules`, `ColorMergeRules` and `AdvancedColorMergeRules`, implementing `IMergeRules`, with `MergeRulesFactory`. `IMergeUtility`, the `*MergeUtility` classes and `MergeUtilityFactory` are removed, and project `CLAUDE.md` §3 is updated to match (ADR-001 §1).
- `BoardSimulation.Step`: slide one cell with direction-ordered processing and one merge per block, resolve the indicator, spawn the next indicator, end check. Today's behaviour reproduced exactly, including the RNG correlation (CC-020) and the bounded-retry spawn (CC-021).
- Old systems untouched in this deliverable; the new code isn't wired in yet.

**Tests (EditMode)**
- Merge tables: every colour × size pair per mode against `CanMerge`/`MergeBlocks`
- All worked examples on [merge-rules](../systems/merge-rules.md#worked-examples) as `Step` cases
- Walls, indicator immovable and unmergeable, merge results not moving again, one merge per target
- Resolve and spawn: resolved block doesn't move on its resolve move; spawn only on empty cells; no spawn when spawning is disabled
- End condition per mode (full board with/without a mergeable pair)
- Determinism: same board, direction and seed give an identical board, events and random state
- Scoring: tier points per merge, total score delta per step
- Characterisation tests for CC-020 and CC-021 named `Pins_CC020_…`/`Pins_CC021_…`

**DoD (beyond template)**
- [ ] Every rule in merge-rules.md has at least one test; tests were written from the spec (noted in the test file header)
- [ ] No `UnityEngine.Object`, `GameManager` or Entities reference in the simulation assembly
- [ ] `Step` and the rule structs compile under Burst: a `[BurstCompile]` static test entry point calls `Step` in an EditMode test

### CC-006.3 — ECS as a thin wrapper
**Sub-issue:** [#15](https://github.com/rtumelty/Cathartic-Colours/issues/15) · **Risk:** High · **Worker:** primary · **Review:** primary

- `BoardStepSystem` (Simulation group) replaces `InputProcessingSystem`, `BlockMovementSystem`, `ResolveNewBlocksSystem`, `SpawnColorSystem` and `GameEndConditionSystem`. It reads the `MoveDirectionComponent`, builds a `BoardState` from block entities, calls `Step`, writes entities back, updates `GameStateComponent`/`ScoreComponent`, and creates `AudioEventComponent`/`MergeEventComponent` entities from `SimEvent`s, only for accepted moves (fixes **CC-023**).
- `ScoringSystem` → `MergeEffectsSystem` (particles only); jelly wobble triggered from presentation, not from simulation (ADR-001 §3–4)
- Explicit ordering for every remaining system, including `AudioSystem`
- **Burst (ADR-001 §5):** `BoardStepSystem` is `[BurstCompile]`. Tier points and the spawn toggle come from an unmanaged `ScoringConfigComponent` singleton created at game start, not from `GameManager`. Systems left managed, with their reasons on this page: `AudioSystem`, `SaveGameSystem`, `MergeEffectsSystem`.
- **CC-024:** remove `MovingBlockTag`, `PendingMergeTag`, `PendingDestructionTag`, `SpawnNextColorRequest`, `GridCellComponent`, the three unused mode tags, the unreachable branches, the no-op copy-back, and the per-frame `File.Exists` in `SaveGameSystem` (delete only when game over first becomes true)
- `SaveGameManager.RestoreBlocks` no longer adds `MovingBlockTag`

**Tests:** EditMode test for the mapping helpers (entity data ↔ `Block`) if they live in the simulation assembly; otherwise covered by the manual regression pass below. A `Fixes_CC023_…` test: a rejected move returns no events.

**DoD (beyond template)**
- [ ] MANUAL regression pass (below) shows parity in all three modes, apart from CC-023
- [ ] [ecs-frame-order](../systems/ecs-frame-order.md) rewritten for the new system set; [presentation](../systems/presentation.md) and [merge-rules](../systems/merge-rules.md) updated

### CC-006.4 — Fix RNG and spawn (CC-020, CC-021)
**Sub-issue:** [#16](https://github.com/rtumelty/Cathartic-Colours/issues/16) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary

- **CC-020:** the colour roll advances the stored `Random`, so colour and next position come from successive draws
- **CC-021:** spawn picks uniformly from the list of empty cells; it fails only when there are none
- `Pins_` tests become `Fixes_CC020_…`/`Fixes_CC021_…` (for example: one empty cell on 6x6 is always filled; the random state after a step differs from the state after colour roll alone)

**DoD (beyond template):** behaviour change noted in merge-rules.md; the old "about 13% failure" note replaced.

### CC-006.5 — Resume without replay (CC-022)
**Sub-issue:** [#17](https://github.com/rtumelty/Cathartic-Colours/issues/17) · **Risk:** High (save semantics) · **Worker:** primary · **Review:** primary

- The save describes the idle state after a move (`waitingForInput = true`); on restore `WaitingForInput` is forced to true, so files written by older builds also resume correctly. No new or removed fields, and `version` stays `"1.0"` (versioning is CC-031).
- First frame after resume runs no step; the saved indicator stays where it was.
- **Tests:** round-trip test in the simulation assembly if save mapping lives there; otherwise MANUAL TP-SAVE cases plus a new case "resume shows the same board and indicator, and nothing changes until the first move".

**DoD (beyond template):** [save-system](../systems/save-system.md) updated; CC-022 known issue removed and its test case added.

### CC-006.6 — Wiki, test plan and roadmap close-out
**Sub-issue:** [#18](https://github.com/rtumelty/Cathartic-Colours/issues/18) · **Risk:** Low · **Worker:** Haiku (lint, index, roadmap), Sonnet (test-plan cases) · **Review:** primary

- Test plan: update the cases affected by CC-020–CC-023 and the frame-order changes; add the CC-022 resume case; mark the regression pass
- Board: close CC-020–CC-024 (#32–#36) as completed by CC-006 with `gh issue close`; confirm the CC-002 (#8) description matches the rescope
- Lint pass on the pages touched; log `lint`

## Tests summary
- **Unit (EditMode, automated):** all rule, step, determinism and bug-fix tests in `CatharticColours.Simulation.Tests`, run from the command line in every deliverable from .1 onwards.
- **System:** no automated ECS tests yet, because the ECS code stays in `Assembly-CSharp` until the rest of CC-002. Covered by the manual regression pass (below) and recorded as a known gap.

## Risk and model routing (approved)
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-006.1 | Medium | Sonnet | Assembly layout and enum move; primary checks compile and asset integrity |
| CC-006.2 | High | Primary (core), Sonnet (tests from spec) | Tests written independently of the code to catch parity mistakes |
| CC-006.3 | High | Primary | System ordering and ECS rewiring |
| CC-006.4 | Medium | Sonnet | Clear spec, tests already in place |
| CC-006.5 | High | Primary | Save semantics |
| CC-006.6 | Low | Haiku + Sonnet | |

## MANUAL (Ronan)
1. **Regression pass after CC-006.3, and again at completion.** In the Unity editor, open the main menu scene, press Play, and run [test-plan.md](../test-plan.md) areas Standard, Color Merge, Advanced Color Merge, Game over and Save/Resume. Report any case that differs from its expected result. (The agent can't play the game.)
2. **Device check at completion.** There's no Android build pipeline yet (CC-004), so the feature-completion build is an editor play-through unless CC-004 has landed by then.

## Decisions
All approved by Ronan 2026-11-18 (D3 with the naming change).
- **D1:** CC-002 is split. CC-006 builds only the simulation assembly and its EditMode tests; CC-002 keeps asmdefs for the remaining game code and PlayMode tests.
- **D2:** The CLI test runs use Unity 6000.6.9f1, after the project upgrade in CC-033. Order: CC-034 → CC-033 → CC-006. CC-006 doesn't start until both are merged; when it does, merge `develop` into this branch first.
- **D3:** Architecture per ADR-001: pure assembly; struct rule sets named `*MergeRules` implementing `IMergeRules` (renamed at Ronan's request, because the `I` prefix belongs only on the interface); one `BoardStepSystem`; score in the simulation; `MergeEffectsSystem` for particles.
- **D4:** Parity first, fixes after. CC-023 and CC-024 are fixed during the wrapper swap because the code they live in is removed.
- **D5:** CC-022 is fixed without a save format change; versioning stays with CC-031.

## Divergences
- 2026-11-18 (planning session, before work started): plan aligned with the project `CLAUDE.md` §3 Burst rule (commit 940c674, brought into `develop` late). `BoardStepSystem` is Burst-compiled, config comes through an unmanaged singleton, the factory dispatches by `switch` without boxing, and a Burst compile check is added to the CC-006.2 DoD. Scope, order and model routing are unchanged. Approved by Ronan on 2026-11-18.

## Implementation notes
- **CC-006.1** (2026-11-19): enums moved with values and underlying types unchanged; `GameConfiguration` and `ColorProfile` assets are byte-for-byte unchanged after a batchmode import (they store the enums as integers). Four smoke tests pin the enum values that saves and assets depend on. Fully-qualified `ECS.Components.GameMode` uses in `GameConfiguration` and `GameRenderer` were shortened. The CLI run writes `TestResults/` inside the Unity project folder, so `TestResults/` is now gitignored at the repo root. The only compiler warning is the existing CS0414 on `JellyManager.wobbleActive`.

- **CC-006.2** (2026-11-19): 200 EditMode tests pass (Sonnet wrote 196 from merge-rules.md and an API contract without reading the code; primary wrote the 3 Burst cases). Specifics:
  - **Old utilities removed in .2 as planned, which meant a small change to two old systems.** The new `Simulation.MergeResult` clashed with `ECS.Utilities.MergeResult`, so `IMergeUtility`, the three `*MergeUtility` classes and `MergeUtilityFactory` were deleted, and `BlockMovementSystem` and `GameEndConditionSystem` now call `MergeRulesFactory.CanMerge`/`Merge` through `BlockComponent.ToSimulationBlock()`. The game therefore already runs the new rule structs (a line-for-line port, covered by the full colour × size tables); the move mechanics are still the old systems until .3. The "old systems untouched" line in the plan is otherwise kept.
  - **Name clash:** the global `Block` MonoBehaviour (`Gameplay/Block.cs`) wins name lookup outside the `Simulation` namespace, so ECS code writes `Simulation.Block`. Recorded in project `CLAUDE.md` §3.
  - **Slide order:** `Step` visits cells in decreasing progress along the direction instead of sorting a list of blocks. That matches the old sort (furthest first; ties are in independent rows or columns) and processes each original block once, because blocks only move into already-visited cells.
  - **Rejection:** `Step` rejects a non-unit direction or a board that is already game over (computed from the board, equivalent to the stored `GameOver` flag), returning no events. A legal move that changes nothing is still accepted and resolves and spawns, as today.
  - **Burst:** `BurstCompatibilityTests` compiles a `[BurstCompile]` probe that runs `MergeRulesFactory.Step` for all three modes and the generic `Step`, checks that it really ran under Burst (a `[BurstDiscard]` marker), and compares it with a managed run. Burst also compiles direct calls to a `[BurstCompile]` method, so the managed run goes through a method without the attribute.
  - **Test seeds:** `Unity.Mathematics.Random`'s first draw from a small seed is small, so seeds 1..200 all roll Red. The reachability test spreads its seeds; the game seeds from `DateTime.Ticks`, which are large.
  - **Found, not fixed:** a resumed game's indicator is given a `MovingBlockTag` by `SaveGameManager.RestoreBlocks`, so on the first move after resume the indicator can slide (fresh indicators can't). The simulation never moves indicators; the .3 change to `RestoreBlocks` and the CC-022 fix in .5 cover it. To check in the .3 regression pass.
  - Spec questions from the test writer, all resolved as the code behaves: Standard would merge two `None`-colour blocks of equal size (they never exist, so that pair is skipped in the table test); the result colour and size of clearing merges are not asserted except Color Merge's White.

- **CC-006.3** (2026-11-19): `BoardStepSystem` (Burst) replaces the five move systems; `ScoringSystem` is now `MergeEffectsSystem`; 208 EditMode tests pass. Specifics:
  - **Entity identity.** `GameRenderer` keys visuals by entity, so the wrapper must not recreate blocks each move. `OccupantTracking.Apply` (simulation assembly, 7 tests) replays the step's events over a cell → entity-index map: slid, merged-into and resolved blocks keep their entity, merged-away blocks are destroyed, new indicators get a new entity. Same as the old systems.
  - **Verified in a real ECS world.** A temporary editor script (not committed) built a world with `BoardStepSystem` and `ClearMoveDirectionSystem`, ran a move with synchronous Burst compilation and checked 16 things: entity identity, score 10 and `CurrentFrameScore` reset, two audio events, merge event points, the resolved indicator, the new indicator, the cleared command, and CC-023 (game-over swipe: no sound, no move counted). All passed with no Burst errors. Real play is still the MANUAL regression pass.
  - **Burst choices, as planned.** `BoardStepSystem` and `ClearMoveDirectionSystem` (also made Burst, since it is trivial) and `EndFrameSystem` are Burst. Managed, with reasons: `AudioSystem` (FMOD `RuntimeManager`), `SaveGameSystem` (file I/O, `JsonUtility`), `MergeEffectsSystem` (calls the `PointsParticleSpawner` MonoBehaviour).
  - **Ordering.** `ClearMoveDirectionSystem` and `AudioSystem` are `[UpdateAfter(BoardStepSystem)]`, so audio now always plays in the move's frame (before, it was unordered and could lag a frame). Presentation: `MergeEffectsSystem` → `SaveGameSystem` → `EndFrameSystem`.
  - **Jelly wobble** is triggered by `GameRenderer.WobbleOnNewMove` when `MoveCount` increases, using the new non-saved `GameStateComponent.LastMoveDirection`. It may start one frame later than before, because MonoBehaviour `Update` and the ECS groups run at different points in the player loop.
  - **Score** is computed in the step; `MergeEventComponent` gained `Points` for the particles. Score no longer depends on the particle spawner existing.
  - **CC-024 removals:** `MovingBlockTag`, `PendingMergeTag`, `PendingDestructionTag`, `SpawnNextColorRequest`, `GridCellComponent`, the three mode tags and also `SpawnColorSystemTag` (its job is now `ScoringConfigComponent.Settings.SpawnEnabled`). The unreachable branches and the no-op copy-back went with their systems. `SaveGameSystem` deletes the save once when the game ends. `RestoreBlocks` no longer adds `MovingBlockTag`. `GameConfiguration.GetPointsForTier` has no callers now; left in place (not in the CC-024 list).
- **Divergences in CC-006.3 (flagged to Ronan):**
  - **CC-022 partly changes here.** The resume bug was a resolve and spawn running in the first frame after resume without a move. Those steps now exist only inside a move, so resume no longer changes the board or indicator. The saved `waitingForInput = false` is still restored, so that first frame saves the file again (harmless). CC-006.5 still does the planned save-semantics fix and its test case.
  - **CC-025 is fixed as a side effect:** `BoardStepSystem` requires every singleton it reads. The issue (#37) is left open for the planning session to close or re-scope.
  - **Restored indicator slide** (found in .2) is gone with `MovingBlockTag`.
  - Unity changed `Assets/Materials/BlockMaterial.mat` (wobble values from playing the game in the editor). Not part of this work and not committed.

- **CC-006.4** (2026-11-19, Sonnet worker, reviewed by primary): 207 EditMode tests pass. The colour roll now advances the stored random state (CC-020), and the spawn counts the empty cells and picks one with a single draw in cell index order, failing only on a full board (CC-021). The `Pins_` tests became `Fixes_` tests in `FixedBugTests.cs`; tests that only made sense for the old try-based spawn (the 72-try boundary and the 13% helper check) were removed, and two tests that skipped or searched seeds for the old failure now check every seed.
  - **Wrapper change that goes with the fix:** `BoardStepSystem` no longer marks a move as unsaved when the spawn fails. With the fix that happens only on a full board that still has a merge, and that move should be saved.
  - **Test seeds** are spread as `(i + 1) * 2654435761u`: small seeds give small first draws, and seed 0 is rejected by `Unity.Mathematics.Random`.

- **CC-006.5** (2026-11-19, primary): `SaveGameSystem` writes `waitingForInput = true` (the idle state after the move) and `SaveGameManager.RestoreWorldState` always sets `WaitingForInput = true`, ignoring the saved value, so files from older builds resume the same way. No field added or removed and `version` stays `"1.0"`; `waitingForInput` is now written but not read. Together with CC-006.3 (no resolve or spawn outside a move), resume changes nothing until the first move, and no longer rewrites the file in its first frame. The save mapping lives in `Assembly-CSharp`, so there is no unit test: covered by MANUAL TP-SAVE-03 (no longer marked known-to-fail) and the new TP-SAVE-07. 207 EditMode tests still pass.

- **CC-006.6** (2026-11-19): test plan updated by a Sonnet worker and corrected by primary: TP-STD-01, TP-OVER-01, TP-OVER-03 (now every direction, and no sound), TP-AUDIO-01 and TP-AUDIO-04 reflect the fixes; new TP-OVER-07 (a free cell always gets the white block) and TP-SAVE-07; a Regression passes section records Ronan's passes. CC-020 to CC-024 (#32 to #36) closed as completed by CC-006; the CC-002 (#8) description already matched the rescope. Haiku lint pass: three stale lines fixed (merge-rules, configuration, presentation), the ADR-001 Burst set noted. Not changed here, for the planning session: `roadmap.md` line 15 marks finished items with strikethrough, which repeats board status.
  - **Found while testing, not fixed here:** an `ObjectDisposedException` from `GameRenderer.CleanUp` in `OnDestroy` when the ECS world is disposed first during scene teardown. It was already there before CC-006 and has been added to CC-025 (#37).

## Feature test plan
1. Command line: EditMode run passes (all simulation tests).
2. Editor: new game in each mode; play 10 moves, including at least one merge per tier reachable early. Score, particles, sounds and jelly wobble as before.
3. Fill a board to game over; swipe again: no move sound (CC-023).
4. Mid-game, go to the main menu, resume: same board and indicator; nothing happens until the first move (CC-022).

## Status log
- 2026-11-18: Planned
- 2026-11-18: Approved by Ronan (D1–D5, D3 with the rule-set rename; model routing). Starts after CC-034 and CC-033 are merged.
