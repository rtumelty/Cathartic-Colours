# Merge rules, scoring, spawning and end conditions

## Purpose
Describes what a move does to the board in each game mode: which blocks merge and into what, how sliding is executed, how merges score, where new blocks come from and when the game ends. When each system runs is in [ecs-frame-order.md](ecs-frame-order.md); where the mode and point values are configured is in [configuration.md](configuration.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`). Coordinates below are `(x, y)` with `x` increasing to the right and `y` upwards; directions are `(1,0)` right, `(-1,0)` left, `(0,1)` up, `(0,-1)` down (`Assets/Scripts/Gameplay/GameInputHandler.cs`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Simulation/IMergeRules.cs`, `MergeResult.cs` | `IMergeRules` (`CanMerge`, `Merge`) and `MergeResult` |
| `Assets/Scripts/Simulation/MergeRulesFactory.cs` | `GameMode` to rule struct (switch, no boxing) |
| `Assets/Scripts/Simulation/StandardMergeRules.cs` | Standard rules |
| `Assets/Scripts/Simulation/ColorMergeRules.cs` | Color Merge rules |
| `Assets/Scripts/Simulation/AdvancedColorMergeRules.cs` | Advanced Color Merge rules |
| `Assets/Scripts/Simulation/BoardSimulation.cs`, `BoardState.cs`, `Block.cs`, `SimEvent.cs`, `StepSettings.cs` | The move step: slide, merge, score, resolve, spawn, end check |
| `Assets/Tests/EditMode/` | Unit tests for all of the above (`CatharticColours.Simulation.Tests`) |
| `Assets/Scripts/Data/GameConfiguration.cs` | Tier point values (`tier1Points`..`tier4Points`) and the spawn toggle, copied into `ScoringConfigComponent` at game start |
| `Assets/Scripts/ECS/Systems/BoardStepSystem.cs` | Runs `Step` for each move and turns its events into audio and merge events ([ecs-frame-order.md](ecs-frame-order.md)) |
| `Assets/Scripts/Simulation/BlockColor.cs`, `BlockSize.cs`, `ScoreTier.cs`, `GameMode.cs` | Block and mode enums (namespace `Simulation`, assembly `CatharticColours.Simulation`, since CC-006) |
| `Assets/Scripts/ECS/Components/BlockComponent.cs` | `BlockComponent` |

## How it works

### Data model
- `BlockColor` values: None 0, Red 1, Green 2, Yellow 3, Blue 4, Magenta 5, Cyan 6, White 7. Primaries are Red/Green/Blue (single bit); secondaries are Yellow (R+G), Magenta (R+B), Cyan (G+B); White is all three bits (`Assets/Scripts/Simulation/BlockColor.cs`).
- `BlockSize`: None 0, Small 1, Medium 2, Large 3.
- A block has `Color`, `Size`, `GridPosition` and `IsNextColorIndicator` (the white placeholder for the next spawn). All three merge utilities refuse to merge any indicator block.

### The merge strategy
Each mode's rules are a struct implementing `IMergeRules` in the `Simulation` assembly (since CC-006.2, replacing the `IMergeUtility` classes). `MergeRulesFactory` switches on `GameMode`: `AdvancedColorMergeRules` for `AdvancedColorMerge`, `ColorMergeRules` for `ColorMerge`, and `StandardMergeRules` for `Standard` and any other value. `BoardStepSystem` calls `MergeRulesFactory.Step(GameModeComponent.Mode, …)`, which runs the generic `BoardSimulation.Step<TRules>` with the matching struct. Everything below is the behaviour of `Step` (`Assets/Scripts/Simulation/BoardSimulation.cs`), pinned by the EditMode tests in `Assets/Tests/EditMode/`; it is the same as the five ECS systems it replaced in CC-006.3.

`IMergeRules` has two methods: `CanMerge(mover, target)` and `Merge(mover, target)`. `Merge` returns a `MergeResult` with `Color`, `Size`, `Tier` and `DestroysBoth`. `mover` is the moving block and `target` is the block in the cell it moves into. If `DestroysBoth` is false the target block takes the result's colour and size and the moving block is destroyed; if true, both blocks are destroyed. The interface covers only merge rules; spawn, end and scoring rules are fixed in `BoardSimulation` (see Known issues).

### Rules per mode

**Standard** (`Assets/Scripts/Simulation/StandardMergeRules.cs`)
- `CanMerge`: same colour and same size, neither an indicator. Any colour (the game only ever spawns Red, Green, Blue, so in practice three independent colour families).
- `MergeBlocks` by the moving block's size:

| Pair | Result | Tier |
|---|---|---|
| Small + Small | Medium, same colour | Tier1 |
| Medium + Medium | Large, same colour | Tier2 |
| Large + Large | both destroyed | Tier3 |

**Color Merge** (`Assets/Scripts/Simulation/ColorMergeRules.cs`)
- `CanMerge` is false for equal colours, for White or None on either side, and for indicators. Size is ignored. It is true for two different primaries, and for a secondary with the primary it lacks (the two share no bits and together make 7). Two secondaries never merge.
- `MergeBlocks` ORs the colour bits. Result size is the **moving block's** size (blocks are only ever Small in this mode).

| Pair | Result | Tier |
|---|---|---|
| Red + Green | Yellow | Tier1 |
| Red + Blue | Magenta | Tier1 |
| Green + Blue | Cyan | Tier1 |
| Yellow + Blue, Magenta + Green, Cyan + Red | both destroyed (result colour White, `DestroysBoth`) | Tier3 |

**Advanced Color Merge** (`Assets/Scripts/Simulation/AdvancedColorMergeRules.cs`)
- `CanMerge` is false for indicators and None, then by size pair:

| Pair | Condition | Result | Tier |
|---|---|---|---|
| Small + Small | same primary colour | Medium, same colour | Tier1 |
| Medium + Medium | two different primaries | Large, OR of the colours (secondary) | Tier2 |
| Large + Medium (either way round) | Large is secondary, Medium is the primary it lacks | Large White | Tier3 |
| Large + Large | both White | both destroyed | Tier4 |

Every other combination does not merge, including Small + Medium, Small of different colours, two Medium of the same colour, and a Large that is not secondary/White. Same-colour Medium pairs therefore have no merge path.

### Sliding and merging
Source: `Assets/Scripts/Simulation/BoardSimulation.cs`, `Slide`.

0. **Rejected moves.** `Step` does nothing (no change, no events, random state untouched) if the direction is not one of the four unit directions or the board is already game over. A legal move that changes nothing is still a move: it resolves and spawns.
1. **Each input moves each block at most one cell.** Each block is examined once per move, so there is no repeated sliding to the wall. This matches the PuzzleScript prototype, whose movement rule `[ > Colours | no Colours ] -> [ | Colours ]` also advances a block one cell per turn (`CatharticColours.pz`, RULES; see [raw sources](../../raw/README.md)). A consequence: a block with free space ahead moves rather than merges, so a merge needs the cell ahead to be occupied when the block is processed.
2. Every block on the board at the start of the move is processed except indicators, which never move.
3. Cells are visited in order of decreasing progress along the direction (for a move right, from the right-hand column to the left), so blocks furthest along the direction of travel are processed first. Blocks in different rows or columns are independent.
4. For each block, the target cell is its cell + direction:
   - **Out of bounds:** the block stays.
   - **Empty:** the block moves one cell. The board updates immediately, so a block behind can move into the cell just vacated.
   - **Occupied and `CanMerge` and the target has not already been merged into this move:** merge. The mover's cell empties; the target takes the result colour and size. If `DestroysBoth`, the target cell empties too.
   - **Occupied otherwise** (cannot merge, or the target was already merged into this move): the block stays.
5. **One merge per block per move:** a mover is gone once it merges, a block that was merged into cannot be merged into again in the same move, and a merge result never moves further in the same move (it sits in a cell already visited).
6. Each move appends a `Moved` event; each merge a `Merged` event (mover cell, target cell, result colour and size, the mover's size, tier, points, `DestroysBoth`). `BoardStepSystem` turns `Merged` events into a `MergeEventComponent` and a merge sound ([ecs-frame-order.md](ecs-frame-order.md#event-entities)).

### Worked examples
Rows only; one row of a grid of width 4 (`x = 0..3`), `.` = empty, the move is right `(1,0)` unless stated. Other rows are independent. `R/G/B` = Red/Green/Blue, `Y/M/C/W` = Yellow/Magenta/Cyan/White; `s/m/l` = Small/Medium/Large. The white indicator block, if present, sits in another cell and is ignored here. Point values assume the default tier points (below).

**Standard**
- `[Rs Rs Bs .]` move right: Bs (x2) moves to x3, then Rs (x1) moves to x2, then Rs (x0) moves to x1 -> `[. Rs Rs Bs]`. No merge, because the cell ahead was empty when each block was processed.
- `[. . Rs Rs]` move right: x3 is blocked by the wall; Rs at x2 targets x3, same colour and size -> `[. . . Rm]`. Tier1, 10 points.
- `[. Rs Rs Rs]` move right: x3 blocked; x2 merges into x3 (Rm); x1 then finds x2 empty and moves -> `[. . Rs Rm]`. Only one merge happens.
- `[. . Rl Rl]` move right: both destroyed -> `[. . . .]`. Tier3, 100 points.

**Color Merge**
- `[Rs Gs . .]` move left: Rs at x0 blocked by wall; Gs at x1 merges into it -> `[Ys . . .]`. Tier1, 10 points.
- `[Ys Bs . .]` move left: Yellow + Blue is a secondary with its missing primary -> both destroyed -> `[. . . .]`. Tier3, 100 points (white particles).
- `[Rs Rs . .]` move left: equal colours do not merge; Rs at x1 stays blocked -> unchanged. `[Ys Ms . .]`: two secondaries do not merge.

**Advanced Color Merge**
- `[. . Rs Rs]` right -> `[. . . Rm]` (Tier1). `[. . Rs Gs]` right: different colours at Small do not merge; Rs stays.
- `[. . Rm Bm]` right: two different primaries at Medium -> `[. . . Ml]` (Magenta Large, 1 OR 4 = 5). Tier2, 50 points.
- `[. . Gm Ml]` right: Large secondary Magenta meets the Medium primary it lacks (Green) -> `[. . . Wl]`. Tier3, 100 points. The mirror `[. . Ml Gm]` right is also legal: Medium Green at x3 is the blocked target, Ml at x2 is the mover.
- `[. . Wl Wl]` right -> `[. . . .]` both destroyed. Tier4, 200 points.
- `[. . Rm Rm]` right: no merge (same colour at Medium has no rule); Rm at x2 stays.

### Scoring
Source: `Assets/Scripts/Simulation/BoardSimulation.cs` (`Slide`), `Assets/Scripts/Simulation/StepSettings.cs` (`TierPoints`), `Assets/Scripts/Data/GameConfiguration.cs`.

- Only merges score; each merge earns `TierPoints.For(tier)`, and `StepResult.ScoreDelta` is the sum for the move. There is no multiplier, chain or per-move bonus. The points come from `GameConfiguration` through `ScoringConfigComponent`; score no longer depends on a particle spawner existing (it did before CC-006.3).
- Defaults: Tier1 10, Tier2 50, Tier3 100, Tier4 200 (`None` gives 0). The configuration assets do not override these ([configuration.md](configuration.md)).
- `BoardStepSystem` adds the delta to `ScoreComponent.TotalScore`; `CurrentFrameScore` is this frame's sum (zero on frames with no merge).
- Each merge also emits particles at the merge cell with the result colour (`MergeEffectsSystem` calls `PointsParticleSpawner.Emit(points, colour, position)`).

| Mode | Tier use |
|---|---|
| Standard | T1 Small pair, T2 Medium pair, T3 Large pair (clears) |
| Color Merge | T1 secondary made, T3 white clear |
| Advanced Color Merge | T1 Small to Medium, T2 Medium to secondary Large, T3 to White Large, T4 White clear |

### Spawning
- **Initial block.** `GameRenderer.SpawnInitialBlock` creates one white indicator at `(gridWidth / 2, gridHeight / 2)` (integer division) when a new game starts, not when resuming (`Assets/Scripts/Gameplay/GameRenderer.cs`).
- **Each move**, after sliding (`BoardSimulation.ResolveIndicators` and `SpawnIndicator`):
  1. Every indicator block on the board becomes a Small block whose colour is Red, Green or Blue with equal probability (`random.NextInt(0, 3)`) and loses the indicator flag. Sliding is already done, so it does not move on the move that resolves it. The draw advances the stored random state, so the spawn below draws from the next state (since CC-006.4, [CC-020](https://github.com/rtumelty/Cathartic-Colours/issues/32)).
  2. If spawning is enabled (`StepSettings.SpawnEnabled`, from `GameConfiguration.spawnNextColourSystem`), a new white Small indicator block is placed on a uniformly random empty cell: the empty cells are counted, one draw `random.NextInt(0, emptyCount)` picks among them in cell index order (`x + y * Width`), and the indicator goes there. Exactly one draw per spawn. Only a board with no empty cell fails: nothing is spawned, no draw is made and `StepResult.SpawnFailed` is true; the move is still saved (since CC-006.4, [CC-021](https://github.com/rtumelty/Cathartic-Colours/issues/33)).
- The indicator is a real block on the board: it blocks movement, never merges, and counts as an occupied cell for game over. Its final colour is decided only when the next move resolves it.
- **Randomness source:** `GameStateComponent.Random` (`Unity.Mathematics.Random`), seeded with `(uint)DateTime.Now.Ticks` at game start and on restart, or restored from the save's `randomState` on resume (`Assets/Scripts/Gameplay/GameRenderer.cs`, `Assets/Scripts/SaveSystem/SaveGameManager.cs`).
- With `spawnNextColourSystem` false, nothing is spawned: the single initial indicator is resolved on the first move and nothing else is ever spawned.

### End conditions
Source: `Assets/Scripts/Simulation/BoardSimulation.cs` (`IsGameOver`, `HasEmptyCell`, `HasMergeablePair`).

- `GameStateComponent` has `WaitingForInput`, `GameOver`, `LevelComplete`, `MoveCount` and `Random`. Only `GameOver` is set by the end-condition logic; `LevelComplete` is never set ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)).
- At the end of every accepted move, `Step` reports game over when **both** hold:
  1. No cell of the `Width x Height` grid is empty (the white indicator counts as occupied), and
  2. No block has a 4-neighbour for which the current mode's `CanMerge(block, neighbour)` is true.
- On game over `Step` appends a `GameOver` event; `BoardStepSystem` sets `GameOver` and `WaitingForInput` to true and creates a `GameOver` audio event. Moves are rejected while `GameOver` is true, with no sound ([CC-023](https://github.com/rtumelty/Cathartic-Colours/issues/35), fixed in CC-006.3).
- The rule is identical for all three modes; only `CanMerge` differs. It does not look at the score or the move count, and clearing the board does not end the game. (The PuzzleScript prototype declares a win when the board is cleared, `CatharticColours.pz`, RULES; the Unity game has no equivalent, see [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31).)
- UI: `GameUIManager.UpdateGameState` shows the status "Grid Full!" and the game-over panel when `GameOver` is true (`Assets/Scripts/Gameplay/GameUIManager.cs`).

## Interactions with other systems
- System ordering, events and singletons: [ecs-frame-order.md](ecs-frame-order.md).
- Mode selection, tier points and spawn toggle come from `GameConfiguration`: [configuration.md](configuration.md).
- Merge events drive particles and score display; audio events drive FMOD one-shots; the save system stores blocks, score and random state. See [save-system.md](save-system.md), [audio-fmod.md](audio-fmod.md) and [presentation.md](presentation.md).

## Known issues
- `IMergeRules` covers merge rules only; spawn policy, end conditions, scoring and move mechanics are fixed in `BoardSimulation`. [CC-008](https://github.com/rtumelty/Cathartic-Colours/issues/20)
- The spawn colour is chosen only on the next move. [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19)
- `LevelComplete` is never set. [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)
- Advanced Color Merge has unmergeable dead ends: two Medium blocks of the same colour, a Small and a Medium, and Small blocks of different colours never merge (`Assets/Scripts/Simulation/AdvancedColorMergeRules.cs`, `CanMerge`). [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38)
- Color Merge result size is the moving block's size and `CanMerge` ignores size; this works only because blocks are always Small in that mode (`Assets/Scripts/Simulation/ColorMergeRules.cs`). [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38)

## Related test cases
- [Standard](../test-plan.md#standard-mode-std): TP-STD-01 to TP-STD-07. [Color Merge](../test-plan.md#color-merge-mode-cm): TP-CM-01 to TP-CM-06. [Advanced Color Merge](../test-plan.md#advanced-color-merge-mode-acm): TP-ACM-01 to TP-ACM-07.
- [HUD](../test-plan.md#gameplay-hud-hud): TP-HUD-05 (score). [Game over](../test-plan.md#game-over-over): TP-OVER-01, TP-OVER-02 (end condition).
