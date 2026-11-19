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
| `Assets/Scripts/Simulation/BoardSimulation.cs`, `BoardState.cs`, `Block.cs`, `SimEvent.cs`, `StepSettings.cs` | Pure move step (slide, merge, score, resolve, spawn, end check); not yet used by the game (wired in by CC-006.3) |
| `Assets/Tests/EditMode/` | Unit tests for all of the above (`CatharticColours.Simulation.Tests`) |
| `Assets/Scripts/ECS/Systems/BlockMovementSystem.cs` | Slide and merge execution |
| `Assets/Scripts/ECS/Systems/ScoringSystem.cs`, `Assets/Scripts/ECS/Components/ScoringComponents.cs` | Merge events, tiers, score |
| `Assets/Scripts/Data/GameConfiguration.cs` | Tier point values (`GetPointsForTier`) |
| `Assets/Scripts/ECS/Systems/SpawnColorSystem.cs`, `ResolveNewBlocksSystem.cs` | Indicator spawn and colour resolve |
| `Assets/Scripts/ECS/Systems/GameEndConditionSystem.cs` | Game over |
| `Assets/Scripts/Simulation/BlockColor.cs`, `BlockSize.cs`, `ScoreTier.cs`, `GameMode.cs` | Block and mode enums (namespace `Simulation`, assembly `CatharticColours.Simulation`, since CC-006) |
| `Assets/Scripts/ECS/Components/BlockComponent.cs` | `BlockComponent` |

## How it works

### Data model
- `BlockColor` values: None 0, Red 1, Green 2, Yellow 3, Blue 4, Magenta 5, Cyan 6, White 7. Primaries are Red/Green/Blue (single bit); secondaries are Yellow (R+G), Magenta (R+B), Cyan (G+B); White is all three bits (`Assets/Scripts/Simulation/BlockColor.cs`).
- `BlockSize`: None 0, Small 1, Medium 2, Large 3.
- A block has `Color`, `Size`, `GridPosition` and `IsNextColorIndicator` (the white placeholder for the next spawn). All three merge utilities refuse to merge any indicator block.

### The merge strategy
Each mode's rules are a struct implementing `IMergeRules` in the `Simulation` assembly (since CC-006.2, replacing the `IMergeUtility` classes). `MergeRulesFactory` switches on `GameMode`: `AdvancedColorMergeRules` for `AdvancedColorMerge`, `ColorMergeRules` for `ColorMerge`, and `StandardMergeRules` for `Standard` and any other value. `BlockMovementSystem` and `GameEndConditionSystem` call `MergeRulesFactory.CanMerge`/`Merge(GameModeComponent.Mode, …)`, converting each `BlockComponent` with `ToSimulationBlock()`. `BoardSimulation.Step` runs a whole move on a `BoardState` with the same rules; it has the same behaviour as the systems described below, pinned by the EditMode tests, and replaces them in CC-006.3.

`IMergeRules` has two methods: `CanMerge(mover, target)` and `Merge(mover, target)`. `Merge` returns a `MergeResult` with `Color`, `Size`, `Tier` and `DestroysBoth`. In `BlockMovementSystem`, `block1` is the moving block and `block2` is the block it moves into (`Assets/Scripts/ECS/Systems/BlockMovementSystem.cs`, `OnUpdate`). If `DestroysBoth` is false the target block takes the result's colour and size and the moving block is destroyed; if true, both blocks are destroyed. The interface covers only merge rules; spawn, end and scoring rules are fixed in systems (see Known issues).

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

### Sliding and merging (`BlockMovementSystem`)
Source: `Assets/Scripts/ECS/Systems/BlockMovementSystem.cs`, `OnUpdate`.

1. **Each input moves each block at most one cell.** The system runs once per move frame and each block is examined once, so there is no repeated sliding to the wall. This matches the PuzzleScript prototype, whose movement rule `[ > Colours | no Colours ] -> [ | Colours ]` also advances a block one cell per turn (`CatharticColours.pz`, RULES; see [raw sources](../../raw/README.md)). A consequence: a block with free space ahead moves rather than merges, so a merge needs the cell ahead to be occupied when the block is processed.
2. Only blocks with `MovingBlockTag` enabled are processed. `InputProcessingSystem` enabled it on every block that has the tag; the white indicator and any block converted this move do not have it enabled.
3. An occupancy map of **all** blocks is built first (`GridPosition` to entity).
4. Moving blocks are sorted with `BlockComparer`, which compares `dot(posB - posA, direction)`. Result: blocks furthest along the direction of travel are processed first. Blocks in different rows or columns are independent.
5. For each block, the target cell is `GridPosition + direction`:
   - **Out of bounds:** the block stays and its tag is disabled.
   - **Empty in the occupancy map:** the block moves one cell (the map is updated immediately, so a block behind can move into the cell just vacated) and its tag is disabled.
   - **Occupied and `CanMerge` and the occupant has not already been merged into this move:** merge. The moving block is destroyed and removed from the map; the occupant takes the result colour and size and is added to `mergedIntoBlocks`. If `DestroysBoth`, both are destroyed and both removed from the map.
   - **Occupied otherwise** (cannot merge, or occupant was already merged into this move): the block stays and its tag is disabled.
6. **One merge per block per move:** a moving block is destroyed when it merges, and a block that was merged into cannot be merged into again in the same move. A merge result also never moves further in the same move.
7. Each merge creates a `MergeEventComponent` (position = target cell, tier, result colour) and an audio event. The sound is `Block_Merge_Large` for destroying merges, otherwise chosen from the moving block's size and the result size (`GetMergeAudioPath`: Small if either is Small, else Medium if either is Medium, else Large).
8. All structural changes are recorded in one `EntityCommandBuffer` played back at the end of the system.

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
Source: `Assets/Scripts/ECS/Systems/ScoringSystem.cs`, `Assets/Scripts/ECS/Components/ScoringComponents.cs`, `Assets/Scripts/Data/GameConfiguration.cs`.

- Only merges score; every `MergeEventComponent` produces `GameConfiguration.GetPointsForTier(tier)` points. There is no multiplier, chain or per-move bonus.
- Defaults: Tier1 10, Tier2 50, Tier3 100, Tier4 200 (`None` gives 0). The configuration assets do not override these ([configuration.md](configuration.md)).
- `ScoreComponent.TotalScore` accumulates; `CurrentFrameScore` is set to this frame's sum on every run of the system (zero on frames with no merge).
- Each event also emits particles at the merge cell with the result colour (`PointsParticleSpawner.Emit(points, colour, position)`).

| Mode | Tier use |
|---|---|
| Standard | T1 Small pair, T2 Medium pair, T3 Large pair (clears) |
| Color Merge | T1 secondary made, T3 white clear |
| Advanced Color Merge | T1 Small to Medium, T2 Medium to secondary Large, T3 to White Large, T4 White clear |

### Spawning
- **Initial block.** `GameRenderer.SpawnInitialBlock` creates one white indicator at `(gridWidth / 2, gridHeight / 2)` (integer division) when a new game starts, not when resuming (`Assets/Scripts/Gameplay/GameRenderer.cs`).
- **Each move**, after movement (`SpawnColorSystem`, which exists only if `GameConfiguration.spawnNextColourSystem` was true at game start):
  1. `ResolveNewBlocksSystem` converts every indicator block on the board into a Small block whose colour is Red, Green or Blue with equal probability (`random.NextInt(0, 3)`), clears the indicator flag, and gives it a disabled `MovingBlockTag`. It therefore does not move on the move that resolves it.
  2. `SpawnColorSystem` creates a new white Small indicator block with `IsNextColorIndicator = true` at a random empty cell: up to `Width * Height * 2` attempts, each picking `x` in `[0, Width)` and `y` in `[0, Height)` and accepting the first unoccupied cell. If none is found it sets `WaitingForInput = true` and spawns nothing.
- The indicator is a real block in the occupancy map: it blocks movement, never merges, and counts as an occupied cell for game over. Its final colour is decided only when the next move resolves it.
- **Randomness source:** `GameStateComponent.Random` (`Unity.Mathematics.Random`), seeded with `(uint)DateTime.Now.Ticks` at game start and on restart, or restored from the save's `randomState` on resume (`Assets/Scripts/Gameplay/GameRenderer.cs`, `Assets/Scripts/SaveSystem/SaveGameManager.cs`).
- With `spawnNextColourSystem` false, `SpawnColorSystem` never runs: the single initial indicator is resolved on the first move and nothing else is ever spawned.

### End conditions
Source: `Assets/Scripts/ECS/Systems/GameEndConditionSystem.cs`.

- `GameStateComponent` has `WaitingForInput`, `GameOver`, `LevelComplete`, `MoveCount` and `Random`. Only `GameOver` is set by the end-condition logic; `LevelComplete` is never set ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)).
- The system runs every frame and returns if `GameOver` is already true. Otherwise `GameOver` is set when **both** hold:
  1. No cell of the `Width x Height` grid is empty (the white indicator counts as occupied), and
  2. No block has a 4-neighbour for which the current mode's `CanMerge(block, neighbour)` is true.
- On game over it also sets `WaitingForInput = true` and creates a `GameOver` audio event. `InputProcessingSystem` ignores moves while `GameOver` is true.
- The rule is identical for all three modes; only `CanMerge` differs. It does not look at the score or the move count, and clearing the board does not end the game. (The PuzzleScript prototype declares a win when the board is cleared, `CatharticColours.pz`, RULES; the Unity game has no equivalent, see [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31).)
- UI: `GameUIManager.UpdateGameState` shows the status "Grid Full!" and the game-over panel when `GameOver` is true (`Assets/Scripts/Gameplay/GameUIManager.cs`).

## Interactions with other systems
- System ordering, events and singletons: [ecs-frame-order.md](ecs-frame-order.md).
- Mode selection, tier points and spawn toggle come from `GameConfiguration`: [configuration.md](configuration.md).
- Merge events drive particles and score display; audio events drive FMOD one-shots; the save system stores blocks, score and random state. See [save-system.md](save-system.md), [audio-fmod.md](audio-fmod.md) and [presentation.md](presentation.md).

## Known issues
- `IMergeRules` covers merge rules only; spawn policy, end conditions, scoring and move mechanics are hard-coded in systems. [CC-008](https://github.com/rtumelty/Cathartic-Colours/issues/20)
- The spawn colour is chosen only on the next move. [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19)
- Rules are executed inside ECS systems that also call presentation code (`ScoringSystem`). [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)
- `LevelComplete` is never set. [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)
- Advanced Color Merge has unmergeable dead ends: two Medium blocks of the same colour, a Small and a Medium, and Small blocks of different colours never merge (`Assets/Scripts/Simulation/AdvancedColorMergeRules.cs`, `CanMerge`). [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38)
- Color Merge result size is the moving block's size and `CanMerge` ignores size; this works only because blocks are always Small in that mode (`Assets/Scripts/Simulation/ColorMergeRules.cs`). [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38)
- In `BlockMovementSystem` the "target was destroyed, move into the space" branch and the `destroyedBlocks.Contains(entity)` skip are unreachable: destroyed blocks are removed from the occupancy map and are always ahead of the mover in sort order (`Assets/Scripts/ECS/Systems/BlockMovementSystem.cs`). [CC-024](https://github.com/rtumelty/Cathartic-Colours/issues/36)
- Random colour/position correlation (stored `Random` not advanced by `ResolveNewBlocksSystem`), and `SpawnColorSystem` can fail to spawn when empty cells remain; both described in [ecs-frame-order.md](ecs-frame-order.md#known-issues) (`Assets/Scripts/ECS/Systems/ResolveNewBlocksSystem.cs`, `Assets/Scripts/ECS/Systems/SpawnColorSystem.cs`). [CC-020](https://github.com/rtumelty/Cathartic-Colours/issues/32), [CC-021](https://github.com/rtumelty/Cathartic-Colours/issues/33)

## Related test cases
- [Standard](../test-plan.md#standard-mode-std): TP-STD-01 to TP-STD-07. [Color Merge](../test-plan.md#color-merge-mode-cm): TP-CM-01 to TP-CM-06. [Advanced Color Merge](../test-plan.md#advanced-color-merge-mode-acm): TP-ACM-01 to TP-ACM-07.
- [HUD](../test-plan.md#gameplay-hud-hud): TP-HUD-05 (score). [Game over](../test-plan.md#game-over-over): TP-OVER-01, TP-OVER-02 (end condition).
