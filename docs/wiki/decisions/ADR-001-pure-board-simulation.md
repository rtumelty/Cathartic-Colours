# ADR-001 — Pure board simulation core with a thin ECS wrapper

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-18 |
| Roadmap id | [CC-006](../roadmap.md#cc-006) |
| Supersedes | — |

## Context
The rules of a move are split across five Simulation-group systems: `InputProcessingSystem`, `BlockMovementSystem`, `ResolveNewBlocksSystem`, `SpawnColorSystem` and `GameEndConditionSystem` (`Assets/Scripts/ECS/Systems/`). They coordinate through `MovingBlockTag`, `WaitingForInput` and partly unconstrained ordering ([ecs-frame-order](../systems/ecs-frame-order.md)). Simulation calls presentation directly (`InputProcessingSystem` → `JellyManager.StartWobble`; `ScoringSystem` → `PointsParticleSpawner`/`GameRenderer`). Merge strategies (`IMergeUtility`, `Assets/Scripts/ECS/Utilities/`) are classes taking `BlockComponent`. All game code compiles into `Assembly-CSharp`, which test assemblies can't reference.

Consequences today: no rule can be unit-tested; bugs CC-020–CC-024 live in the seams between systems; preview (CC-007), the mode framework (CC-008) and the puzzle solver (CC-009) all need to run a move without a world.

## Options

### Option A — Keep the systems and add tests through ECS worlds
- **Description:** Move game code into asmdefs (all of CC-002) and test the existing systems by building test worlds.
- **Pros:** Least code churn.
- **Cons:** Tests stay slow and indirect; the solver still can't simulate without a world; the seams and their bugs stay.

### Option B — Pure simulation assembly, ECS as a thin wrapper (proposed)
- **Description:** A new assembly `CatharticColours.Simulation` (namespace `Simulation`, folder `Assets/Scripts/Simulation/`) references only `Unity.Mathematics` and `Unity.Collections`. It holds a `BoardState` (width, height, `NativeArray` of cells), the block value types and enums, the merge rules, and `BoardSimulation.Step(ref board, direction, rules, ref random, events) → StepResult`. One ECS system copies entities into a `BoardState`, calls `Step`, writes the result back and turns the returned events into the existing event entities.
- **Pros:** Rules are deterministic and testable in EditMode without a world. One place defines a move, so ordering bugs go away. Reusable by preview, modes and solver. Blittable data keeps the option of Burst for the solver.
- **Cons:** Copying the board in and out each move (trivial at 6x6, once per move). Enums move assembly and namespace (`ECS.Components` → `Simulation`), so `using` lines change across the code.

### Option C — Pure simulation in managed C# classes
- **Description:** As B, but with classes and managed arrays.
- **Pros:** Simplest code to write.
- **Cons:** Can't be Burst-compiled; GC allocations per move and per solver node (CC-009).

## Decision
Option B. It's the smallest change that makes rules testable and reusable, and blittable data keeps the solver fast later.

Specifics:
1. **Rules stay strategies, renamed to fit struct use.** The rule sets become structs named `StandardMergeRules`, `ColorMergeRules` and `AdvancedColorMergeRules`. The `I` prefix stays only on the real interface they implement, `IMergeRules`, which serves as the generic constraint. `IMergeUtility`, the `*MergeUtility` classes and `MergeUtilityFactory` are removed. `MergeRulesFactory` picks by `GameMode` for non-generic callers. `Step` is generic over the rule type (`where TRules : struct, IMergeRules`), so a later Burst caller pays no virtual call. (Naming per Ronan's D3 feedback, 2026-11-18.)
2. **One move system.** `BoardStepSystem` replaces the five Simulation-group systems above. `AudioSystem`, `SaveGameSystem` and `EndFrameSystem` stay. `MovingBlockTag` is removed: the simulation decides which blocks move.
3. **Score in the simulation.** Tier points are passed into `Step` (copied from `GameConfiguration`), and `StepResult` carries the score delta. `ScoringSystem` becomes `MergeEffectsSystem` (Presentation group): it only turns `MergeEventComponent`s into particles and destroys them. Score no longer depends on a particle spawner existing.
4. **Presentation is driven by events.** Jelly wobble and particles react to state or events; no simulation code references `GameManager` presentation objects.
5. **Determinism.** `Step` uses only its inputs (board, direction, rules, tier points, `Unity.Mathematics.Random`). Same inputs give the same result and the same random state.

## Consequences
- Rules are covered by EditMode unit tests in a `CatharticColours.Simulation.Tests` assembly. ECS glue is checked by the manual test plan until CC-002 brings PlayMode tests.
- `Assembly-CSharp` references the new assembly automatically; nothing else moves out of `Assembly-CSharp` (the rest of CC-002 is unchanged).
- Project `CLAUDE.md` §3 gains the `Simulation` namespace and the "rules run in the pure simulation; ECS wraps it" pattern.
- System pages to rewrite: [ecs-frame-order](../systems/ecs-frame-order.md), [merge-rules](../systems/merge-rules.md); to update: [save-system](../systems/save-system.md), [presentation](../systems/presentation.md), [configuration](../systems/configuration.md).
- Follow-ups: CC-007 (preview reads the next indicator from the simulation), CC-008 (generalises the strategy beyond merges), CC-009 (solver calls `Step`, possibly Burst-compiled).

## Status
Accepted, 2026-11-18, with the CC-006 plan. Decision §1 naming changed at Ronan's request: the struct rule sets drop the `Utility` names, and only the interface keeps the `I` prefix.
