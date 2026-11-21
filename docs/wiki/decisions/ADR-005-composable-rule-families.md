# ADR-005 — Game modes as composable rule families

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-21 |
| Roadmap id | [CC-008](https://github.com/rtumelty/Cathartic-Colours/issues/20) |
| Supersedes | — |

## Context
A game mode today is only a choice of merge rules. `IMergeRules` structs (`Assets/Scripts/Simulation/StandardMergeRules.cs` and the two colour modes) are passed as a generic constraint to `BoardSimulation.Step<TRules>`, and `MergeRulesFactory` switches on `GameMode` to pick one. Spawning, scoring and the end condition are fixed inside `BoardSimulation`.

Planned modes need more than that:
- different spawning: weighted colours (CC-035), the next colour chosen at spawn time (CC-007), no spawns (puzzle, CC-009);
- other end conditions and timers (CC-010);
- board changes during play (CC-011, CC-012).

Constraints: the simulation must stay pure and Burst-compatible (ADR-001), with no managed interfaces or statics in systems. The existing modes must replay exactly, including random draws, because saves store the random state.

## Options

### Option A — Everything generic
- **Description:** `Step<TMerge, TSpawn, TScore, TEnd, …>` with a struct per family, and a factory that names every combination.
- **Pros:** no switch at run time; the compiler inlines everything.
- **Cons:**
  - One compiled copy and one factory `case` per combination.
  - Adding a rule to any family means touching the factory for every mode that might use it.
  - The gain is negligible, since spawn, score and end rules run once per move, not per cell.

### Option B — Merge rules generic, other families picked by enum
- **Description:** merge rules keep the generic struct pattern, because they run in the per-cell inner loop. Each other family (spawn, scoring, end condition, grid change, timer) has a struct per rule and an enum. A small static dispatcher per family switches on the enum. An unmanaged `GameRules` struct holds one enum value per family plus the rules' settings, and `GameRules.For(GameMode, settings)` builds each mode.
- **Pros:**
  - A mode is data: one choice per family.
  - Families vary independently, so a new rule is a struct, an enum value and a `case` in that family's dispatcher.
  - Burst-compatible, and fits the existing pattern.
- **Cons:** an enum switch per family per move (negligible cost); two dispatch styles to explain.

### Option C — Function pointers
- **Description:** Burst `FunctionPointer<T>` per family.
- **Pros:** open-ended; no switch.
- **Cons:**
  - Pointers must be compiled and stored at start-up, they're harder to test in managed code, and they can't be stored in saves.
  - Unneeded for a fixed, known set of rules.

### Option D — Do nothing
- **Description:** keep adding `if (mode == …)` branches inside `BoardSimulation`.
- **Cons:** each new mode makes the shared step harder to read and test; spawn changes for one mode risk the others.

## Decision
**Option B.** It keeps the per-cell merge loop as fast as today and makes a mode a choice per family, with no combination factory. Each rule stays a plain, testable struct, and the whole thing stays Burst-compatible.

## Consequences
- **Step order is fixed:** slide and merge (merge and scoring rules) → resolve indicators (spawn rule) → grid change → spawn (spawn rule) → end check. `BoardSimulation.Advance` is the entry point for time passing (timer rule).
- **Spawn rules own every random draw they make,** in a documented order. A rule that changes the order changes replays, and needs new recorded-game hashes, which must be a deliberate decision.
- **Rules aren't saved.** They are derived from the saved mode and settings. A mode whose rules carry state (a timer) adds that state to the save in its own feature.
- **Follow-ups:** CC-007 and CC-035 become new spawn rules. CC-009, CC-010, CC-011 and CC-012 add rules to their families. CC-026's fixes are merge-rule changes.
- **Documented in:** [merge-rules](../systems/merge-rules.md) ("Rule families", "How to add a mode") and [ecs-frame-order](../systems/ecs-frame-order.md).

## Status
Accepted with the CC-008 plan, 2026-11-21.
