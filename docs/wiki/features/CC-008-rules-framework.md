# CC-008 — Game mode rules framework

| Field | Value |
|---|---|
| Issue | [#20](https://github.com/rtumelty/Cathartic-Colours/issues/20) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-008-rules-framework` (from `develop`, created by the planning session on approval) |
| PR | [#96](https://github.com/rtumelty/Cathartic-Colours/pull/96) |
| Decision | [ADR-005](../decisions/ADR-005-composable-rule-families.md) (accepted with this plan, 2026-11-21) |
| Unblocks | [CC-035](https://github.com/rtumelty/Cathartic-Colours/issues/55) (spawn weighting), [CC-009](https://github.com/rtumelty/Cathartic-Colours/issues/21) (puzzle), [CC-010](https://github.com/rtumelty/Cathartic-Colours/issues/22) (time attack), [CC-011](https://github.com/rtumelty/Cathartic-Colours/issues/23) (shifting size), [CC-012](https://github.com/rtumelty/Cathartic-Colours/issues/24) (hazards), and makes [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19) a spawn-rule change |
| Related | [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38) (rule gaps, design input; not changed here), [merge-rules](../systems/merge-rules.md), [ADR-001](../decisions/ADR-001-pure-board-simulation.md) |

## Goal
A game mode becomes a set of swappable rules: how blocks merge, what spawns and where, how merges score, when the game ends, plus the slots later modes need (timers, grid changes). The three existing modes are rebuilt from these rules and **play exactly as before**: same boards, scores, random draws and saves.

## Today (verified on `develop`, 2026-11-21)
- **Merge rules** are already swappable: `IMergeRules` structs (`StandardMergeRules`, `ColorMergeRules`, `AdvancedColorMergeRules`) used as generic constraints. `MergeRulesFactory` switches on `GameMode`.
- **Everything else is fixed** in `BoardSimulation.Step` (`Assets/Scripts/Simulation/BoardSimulation.cs`). The fixed parts:
  - **Spawn:** `ResolveIndicators` makes each indicator a Small Red, Green or Blue block (one draw each), then `SpawnIndicator` puts a new indicator on a random empty cell (one draw). `StepSettings.SpawnEnabled` turns spawning off.
  - **Scoring:** `TierPoints.For(merge.Tier)` inside `Slide`.
  - **End:** `IsGameOver` = no empty cell and no mergeable pair.
  - **Order:** slide → resolve indicators → spawn → end check.
- **ECS:** `BoardStepSystem` reads `GameModeComponent` and `ScoringConfigComponent` (tier points and the spawn toggle), which `GameRenderer` creates from `GameConfiguration`. Saves store `gameMode` and `spawnNextColourSystem`.
- **Safety net already there:** determinism, scoring, end-condition and Burst tests per mode (`StepDeterminismAndScoringTests`, `StepEndConditionTests`, `BurstCompatibilityTests`, …).

## Approach
- **D2 — Rule families** ([ADR-005](../decisions/ADR-005-composable-rule-families.md)):
  - Each family is an interface with struct implementations, the same pattern as `IMergeRules`: `ISpawnRule`, `IScoringRule`, `IEndCondition`, plus `IGridMutation` and `ITimerRule` with only a "none" implementation for now.
  - **Merge rules stay generic** (`Step<TMerge>`), because they run in the per-cell inner loop.
  - **The other families are picked by an enum switch** once per move, each family on its own. Generics for every family would mean one compiled copy per combination, and a factory switch that grows with every mode. A switch per family keeps a new mode to "pick one rule from each family", and is just as Burst-friendly.
- **D3 — `GameRules`, an unmanaged struct** that names a rule from each family and holds their settings (tier points, spawn on or off). `GameRules.For(GameMode, settings)` builds the three existing modes. `BoardSimulation.Step(ref board, direction, in GameRules rules, ref random, events)` replaces the factory switch. `MergeRulesFactory` goes away, or stays as a thin wrapper if the tests still need it.
- **D4 — Fixed step order with named phases:**
  1. slide and merge (merge rule, scoring rule);
  2. resolve the indicators (spawn rule);
  3. **grid change** (new slot; none for now);
  4. spawn (spawn rule);
  5. end check (end condition).

  The spawn rule owns both colour and position draws, and must make them in today's order, so seeds and saves replay the same. A **time entry point** (`BoardSimulation.Advance(ref board, elapsed, in GameRules, …)`) exists for CC-010 but does nothing with the "no timer" rule.
- **D5 — No save change.** Rules are derived from the saved `gameMode` and `spawnNextColourSystem`, never stored. The save format stays `"1.0"`.
- **D6 — Behaviour proven unchanged by recorded games.** Before any refactor, a test plays 50 seeded games per mode (random moves, until game over or 300 moves). Each move's board, score, events and random state go into a hash committed as test data. After the refactor, the same hashes must come out. That's on top of the existing tests, all of which must still pass unchanged (a test may be renamed or retargeted at `GameRules`, never have its expected values changed).
- **Out of scope:**
  - CC-026's rule gaps: the dead ends stay. Fixing them later becomes a merge-rule change only.
  - New modes, CC-007 and CC-035 (they build on this).
  - `LevelComplete` (CC-019).
  - UI text per mode stays as it is.
- **Assembly:** all rules in `Simulation` (pure, Burst). ECS wiring in Core. Nothing in Presentation except where `GameRenderer` creates the singleton.

## Deliverables

### CC-008.1 ([#92](https://github.com/rtumelty/Cathartic-Colours/issues/92)) — Recorded-game tests that pin today's behaviour
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. A recorder that plays seeded games per mode through today's `MergeRulesFactory.Step` and hashes each move (board, score delta, events, random state, game over).
2. The recorded hashes are committed as a test data file. A test replays and compares them. It also runs with spawning off.
3. A short note on the merge-rules page: how to re-record, and that a changed hash means changed behaviour.

**Tests:** the replay test, for 3 modes × spawn on/off. It must pass on today's code before CC-008.2 starts.

**Definition of Done** (template plus):
- [ ] Hash file committed; replay passes on unchanged code; full EditMode suite passes

### CC-008.2 ([#93](https://github.com/rtumelty/Cathartic-Colours/issues/93)) — Rule families and `GameRules` in the simulation
**Risk:** High · **Worker:** primary (core simulation and the pattern every later mode uses) · **Review:** primary
1. The family interfaces and today's rules as structs:
   - `UniformPrimarySpawn` (today's resolve and spawn, plus `NoSpawn` for the spawn toggle);
   - `TierPointsScoring`;
   - `NoMovesLeftEnd`;
   - `NoGridMutation`, `NoTimer`.
2. `GameRules` and `GameRules.For(mode, settings)`.
3. `Step` takes `GameRules` and runs the phases in D4 order. Add `Advance`.
4. Move the existing tests onto `GameRules` where they call the factory, with expected values unchanged.
5. ADR-005 accepted. The merge-rules page gets a "rule families" section and "how to add a mode".

**Tests (EditMode):**
- recorded games still match (CC-008.1);
- every existing simulation test passes;
- Burst test covers `GameRules` for all three modes;
- each new rule struct has unit tests (spawn draw order, scoring per tier, end condition, the none rules change nothing);
- `Advance` with `NoTimer` changes nothing.

**Definition of Done** (template plus):
- [ ] No expected value in an existing test changed; recorded hashes identical
- [ ] ADR-005 and merge-rules page updated

### CC-008.3 ([#94](https://github.com/rtumelty/Cathartic-Colours/issues/94)) — ECS uses `GameRules`
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. A `GameRulesComponent` singleton replaces `ScoringConfigComponent`. `GameRenderer` builds it from `GameConfiguration` for new games and for resumed ones.
2. `BoardStepSystem` calls the new `Step`. `GameModeComponent` stays for the UI and saves.
3. Update the ecs-frame-order and configuration pages.

**Tests:**
- `BoardStepSystemTests` pass unchanged;
- a new test checks that a resumed game gets the same rules as a new one;
- PlayMode smoke tests pass.

**Definition of Done:**
- [ ] EditMode and PlayMode pass; no compile warnings

### CC-008.4 ([#95](https://github.com/rtumelty/Cathartic-Colours/issues/95)) — Wrap-up, device build and PR
**Risk:** Low · **Worker:** Haiku · **Review:** primary
- Feature test plan folded into `test-plan.md`: a short regression pass of each mode, plus resuming an old save.
- Index and log.
- **Device build at the PR** (user-level §2.3).
- PR and quiz.

## Workflow exception (Ronan, 2026-11-21)
- No approval stops between deliverables, and no manual checks. The session works straight through and opens one PR at the end.
- Each sub-issue goes to In progress when work starts, and In review when its commit is pushed. The planning session closes them after the merge.
- Long builds don't hold up commits.
- **Stop and tell Ronan over Remote Control** if keeping behaviour unchanged turns out to need a design choice, or a recorded hash can't be kept. Record it on this page.
- Runs in the sandbox (`E:\Projects\Cathartic-Colours`); no other feature session is active.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-008.1 | Medium | Sonnet | Tests for existing behaviour |
| CC-008.2 | High | primary | Core simulation; sets the pattern for every later mode |
| CC-008.3 | Medium | Sonnet | Contained ECS wiring against a clear spec |
| CC-008.4 | Low | Haiku | Docs, build and PR |

## MANUAL (Ronan)
None during the work. During PR review: play a few moves in each mode, and resume a game saved before the update.

## Decisions
- **D1 — Workflow without stops** (Ronan, 2026-11-21), as CC-039 and CC-042.
- **D2 to D6** as in Approach (proposed by planning; approved with the plan, 2026-11-21).

## Divergences
Implementation specifics only; no change to scope, decisions, deliverable order or model routing.
- **CC-008.1:** the recorder test that rewrites the hash file is `[Explicit]` **and** needs `CC_RECORD_GAMES=1`, because a class-wide `-testFilter` also runs Explicit tests and would quietly re-record. Spawn-off games always run the full 300 moves (nothing spawns, so the board never fills); they still pin sliding, merging and scoring.
- **CC-008.2:**
  - `MergeRulesFactory` is removed (the plan allowed either). Its uses moved to `GameRules.For(mode, settings)` with `BoardSimulation.Step`, `BoardSimulation.IsGameOver(board, in GameRules)`, `GameRules.CanMerge` and `GameRules.MergeBlocks` (named so because `Merge` is the merge-rule field). Tests that said "factory" in their names were renamed to "GameRules"; no expected value changed.
  - `GameRules` also names the merge rule (`MergeRuleKind`), so one value describes a whole mode. The generic `Step<TMerge>(…, in TMerge, in GameRules, …)` stays public, for the generic-agrees-with-dispatch tests and the Burst probe; it ignores `GameRules.Merge`.
  - `NoSpawn` still resolves indicators already on the board (one colour draw each), because the spawn toggle did that before.
  - The scoring dispatcher is called once per merge rather than once per move. Merges are few per move, and it keeps a scoring rule a per-merge function.
  - `BoardStepSystem` was moved onto `GameRules.For(mode, settings)` in CC-008.2 only so it compiles; CC-008.3 replaces `ScoringConfigComponent` with `GameRulesComponent`.
  - `StepSettings` stays as the per-game settings from configuration that `GameRules.For` reads.
- **CC-008.3:**
  - The one builder for new and resumed games is `GameRulesComponent.From(GameConfiguration)`, a static method on the component in Core (`ECS/Components`), so no new folder or assembly reference.
  - `BoardStepSystem` no longer requires `GameModeComponent`, since it only needs the rules. The component stays for the UI and saves.
  - `GameRenderer` itself isn't EditMode-testable. The new tests cover the builder and the configuration a resume produces (`SaveGameManager.InitializeFromSave`), and the PlayMode smoke tests cover `InitializeGame`. `BoardStepSystemTests` changed one test name (ScoringConfig to GameRules), no values.

## Feature test plan
1. Each mode: play to a few merges; scores and merges as before.
2. Resume a game saved on the previous build: board, score and next spawn as before.
3. Spawn toggle off in the menu: no new blocks spawn.

Cases: TP-STD-11, TP-CM-07, TP-ACM-08 (regression per mode), TP-SAVE-12 (resume a save from the previous build), TP-MENU-07 (spawn toggle off, re-run).

## Status log
- 2026-11-21: Planned and approved by Ronan; sub-issues #92-#95
