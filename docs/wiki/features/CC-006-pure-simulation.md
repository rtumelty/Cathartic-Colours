# CC-006 — Extract pure board simulation from ECS

| Field | Value |
|---|---|
| Type | Chore |
| Severity / impact | High |
| Complexity | L |
| Status | Approved |
| Branch | `feature/CC-006-pure-simulation` (from `develop`, created by the planning session on approval) |
| PR | — |
| Roadmap | [CC-006](../roadmap.md#cc-006) |
| Depends on | [CC-034](../roadmap.md#cc-034) (GitHub Issues tracking) and [CC-033](../roadmap.md#cc-033) (Unity 6000.6.9f1 upgrade), both merged first |
| Absorbs | [CC-020](../roadmap.md#cc-020), [CC-021](../roadmap.md#cc-021), [CC-022](../roadmap.md#cc-022), [CC-023](../roadmap.md#cc-023), [CC-024](../roadmap.md#cc-024); part of [CC-002](../roadmap.md#cc-002) |
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
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary

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
**Risk:** High · **Worker:** primary implements the core; Sonnet writes the characterisation tests from the wiki spec without looking at the new implementation · **Review:** primary

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
**Risk:** High · **Worker:** primary · **Review:** primary

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
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary

- **CC-020:** the colour roll advances the stored `Random`, so colour and next position come from successive draws
- **CC-021:** spawn picks uniformly from the list of empty cells; it fails only when there are none
- `Pins_` tests become `Fixes_CC020_…`/`Fixes_CC021_…` (for example: one empty cell on 6x6 is always filled; the random state after a step differs from the state after colour roll alone)

**DoD (beyond template):** behaviour change noted in merge-rules.md; the old "about 13% failure" note replaced.

### CC-006.5 — Resume without replay (CC-022)
**Risk:** High (save semantics) · **Worker:** primary · **Review:** primary

- The save describes the idle state after a move (`waitingForInput = true`); on restore `WaitingForInput` is forced to true, so files written by older builds also resume correctly. No new or removed fields, and `version` stays `"1.0"` (versioning is CC-031).
- First frame after resume runs no step; the saved indicator stays where it was.
- **Tests:** round-trip test in the simulation assembly if save mapping lives there; otherwise MANUAL TP-SAVE cases plus a new case "resume shows the same board and indicator, and nothing changes until the first move".

**DoD (beyond template):** [save-system](../systems/save-system.md) updated; CC-022 known issue removed and its test case added.

### CC-006.6 — Wiki, test plan and roadmap close-out
**Risk:** Low · **Worker:** Haiku (lint, index, roadmap), Sonnet (test-plan cases) · **Review:** primary

- Test plan: update the cases affected by CC-020–CC-023 and the frame-order changes; add the CC-022 resume case; mark the regression pass
- Roadmap: CC-020–CC-024 → Done (absorbed by CC-006); CC-002 description confirmed as rescoped
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
- 2026-11-18 (planning session, before work started): plan aligned with the project `CLAUDE.md` §3 Burst rule (commit 940c674, brought into `develop` late). `BoardStepSystem` is Burst-compiled, config comes through an unmanaged singleton, the factory dispatches by `switch` without boxing, and a Burst compile check is added to the CC-006.2 DoD. Scope, order and model routing are unchanged. Flagged to Ronan.

## Feature test plan
1. Command line: EditMode run passes (all simulation tests).
2. Editor: new game in each mode; play 10 moves, including at least one merge per tier reachable early. Score, particles, sounds and jelly wobble as before.
3. Fill a board to game over; swipe again: no move sound (CC-023).
4. Mid-game, go to the main menu, resume: same board and indicator; nothing happens until the first move (CC-022).

## Status log
- 2026-11-18: Planned
- 2026-11-18: Approved by Ronan (D1–D5, D3 with the rule-set rename; model routing). Starts after CC-034 and CC-033 are merged.
