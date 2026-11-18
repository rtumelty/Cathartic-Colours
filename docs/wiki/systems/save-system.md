# Save system

## Purpose
Describes how a game in progress is written to disk after each move, how the main menu offers to resume it, and how the saved state is turned back into configuration, singletons and block entities. There is one save slot: a single JSON file that is overwritten after every move and deleted when the game ends.

All paths are relative to the Unity project folder (`Cathartic Colours/`). Frame ordering of the systems involved is in [ecs-frame-order.md](ecs-frame-order.md); the start-up path that calls the restore code is in [configuration.md](configuration.md).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/SaveSystem/SaveData.cs` | `SaveGameData` and `SavedBlock` (the serialised schema) |
| `Assets/Scripts/SaveSystem/SaveGameManager.cs` | Static helper: `HasSaveGame`, `LoadSaveGame`, `DeleteSaveGame`, `InitializeFromSave`, `RestoreWorldState`, `RestoreBlocks` |
| `Assets/Scripts/ECS/Systems/SaveGameSystem.cs` | Writes and deletes the save file (Presentation group) |
| `Assets/Scripts/Gameplay/MainMenuController.cs` | Shows the resume button, sets the `LoadFromSave` PlayerPrefs flag (`ResumeGame`) |
| `Assets/Scripts/Gameplay/GameRenderer.cs` | Reads the flag and calls the restore code (`InitializeGame`) |

## How it works

### File location and format
- Path: `Path.Combine(Application.persistentDataPath, "savegame.json")`. The path is built twice, independently: `SaveGameManager.SaveFilePath` and `SaveGameSystem.GetSaveFilePath` (`Assets/Scripts/SaveSystem/SaveGameManager.cs`, `Assets/Scripts/ECS/Systems/SaveGameSystem.cs`).
- Format: `JsonUtility.ToJson(saveData, true)` (pretty-printed) written with `File.WriteAllText`; read with `File.ReadAllText` and `JsonUtility.FromJson<SaveGameData>` (`SaveGameSystem.SaveGame`, `SaveGameManager.LoadSaveGame`). Enums (`GameMode`, `BlockColor`, `BlockSize`) are serialised by `JsonUtility` as integers.
- `SaveGame` creates the directory if missing, but does not write to a temporary file first (a write is one `WriteAllText` call).

### Schema (`SaveGameData`)
`Assets/Scripts/SaveSystem/SaveData.cs`. "Written from" is `SaveGameSystem.CreateSaveData`; "On restore" is what `SaveGameManager` does with it.

| Field | Type | Written from | On restore |
|---|---|---|---|
| `version` | string, default `"1.0"` | constant in the class | **Ignored**: never read or compared |
| `saveTimestamp` | string | constructor: `DateTime.Now.ToBinary().ToString()` | **Ignored** |
| `gameMode` | `GameMode` | `GameManager.ActiveConfiguration.gameMode` | `InitializeFromSave` copies it into a new runtime `GameConfiguration` |
| `gridWidth` | int | `ActiveConfiguration.gridWidth` | Same (`InitializeFromSave`) |
| `gridHeight` | int | `ActiveConfiguration.gridHeight` | Same |
| `spawnNextColourSystem` | bool | `ActiveConfiguration.spawnNextColourSystem` | Same |
| `colorProfileName` | string | `ActiveColorProfile.name` (the runtime copy, so it includes the "(Clone)" suffix; inferred from `Object.Instantiate` in `GameManager.Initialize`) | **Ignored**: `InitializeFromSave` re-instantiates the *current* colour profile (`colorProfileTemplate`) |
| `waitingForInput` | bool | `GameStateComponent.WaitingForInput` | `RestoreWorldState` writes it back |
| `gameOver` | bool | `GameStateComponent.GameOver` | Written back |
| `levelComplete` | bool | `GameStateComponent.LevelComplete` | Written back |
| `moveCount` | int | `GameStateComponent.MoveCount` | Written back |
| `randomState` | uint | `GameStateComponent.Random.state` | `new Random(randomState)` (see [CC-020](../roadmap.md#cc-020): the stored state is not advanced by colour picks) |
| `totalScore` | int | `ScoreComponent.TotalScore` | Written back |
| `currentFrameScore` | int | `ScoreComponent.CurrentFrameScore` | Written back into `ScoreComponent`, but nothing reads it later (`ScoringSystem` overwrites it every frame) |
| `blocks` | `SavedBlock[]` | every entity with `BlockComponent` (`QueryBuilder().WithAll<BlockComponent>()`) | `RestoreBlocks` creates one entity per element |

`SavedBlock` (struct): `color` (`BlockColor`), `size` (`BlockSize`), `gridX`, `gridY` (ints), `isNextColorIndicator` (bool). All five are restored into `BlockComponent`.

Not saved: tier points and any other `GameConfiguration` fields beyond the four above (they come from the current runtime copy on resume), pending event entities, the pending `MoveDirectionComponent`, and `MovingBlockTag` state.

### `SaveGameManager`
`Assets/Scripts/SaveSystem/SaveGameManager.cs` (`static class`, namespace `SaveSystem`).

| Method | Behaviour |
|---|---|
| `HasSaveGame()` | `File.Exists(SaveFilePath)`. No content check |
| `LoadSaveGame()` | Returns `null` (with a warning) if the file is absent. Otherwise reads and deserialises it inside `try/catch`; any exception is logged with `Debug.LogError("Failed to load save game: ...")` and `null` is returned. In the editor it logs the path |
| `DeleteSaveGame()` | Deletes the file if present and logs "Save game deleted". **Not called by any code**: `SaveGameSystem` has its own private `DeleteSaveGame` (see Known issues) |
| `InitializeFromSave(saveData, configTemplate, colorProfileTemplate)` | Logs an error and returns if `saveData` is null. Otherwise instantiates `configTemplate`, overwrites `gameMode`, `gridWidth`, `gridHeight`, `spawnNextColourSystem` from the save, and calls `GameManager.Initialize(runtimeConfig, colorProfileTemplate)` (which instantiates both again) |
| `RestoreWorldState(saveData)` | Returns if `saveData` is null. Using `World.DefaultGameObjectInjectionWorld`: if a `GameStateComponent` entity exists, overwrites `WaitingForInput`, `GameOver`, `LevelComplete`, `MoveCount`, `Random`; if a `ScoreComponent` entity exists, replaces it with the saved totals; then calls `RestoreBlocks` |
| `RestoreBlocks` (private) | Returns if the array is null or empty. Creates one entity per saved block from an archetype of `BlockComponent` + `MovingBlockTag`, sets the data, then **disables** `MovingBlockTag` (so the indicator block gets a tag it does not have in a fresh game; see [CC-022](../roadmap.md#cc-022)) |

There is no validation of restored data: grid positions outside the saved width/height, overlapping blocks, or a `blocks` array of an unexpected size are all accepted as-is.

### Trigger points (`SaveGameSystem`)
`Assets/Scripts/ECS/Systems/SaveGameSystem.cs`: `[UpdateInGroup(PresentationSystemGroup)]`, `[UpdateAfter(ScoringSystem)]`, `RequireForUpdate<GameStateComponent>`. Each frame `OnUpdate` reads `GameStateComponent` and:

| Condition | Action |
|---|---|
| `!WaitingForInput && !GameOver && !LevelComplete` | `SaveGame` (write the file) |
| else if `GameOver || LevelComplete` | `DeleteSaveGame` (private; deletes the file if it exists, `try/catch` with `Debug.LogError`) |
| otherwise (idle: `WaitingForInput` and not over) | nothing |

In practice (see the frame walk-through in [ecs-frame-order.md](ecs-frame-order.md#one-player-move-frame-by-frame)): `WaitingForInput` is false only in the one frame in which `InputProcessingSystem` accepted a move, because `EndFrameSystem` (last in the Presentation group, after `SaveGameSystem`) sets it back to true. So the file is written **once per accepted move**, after the move, merges, scoring, indicator resolve and new indicator spawn have all happened in that frame. If `SpawnColorSystem` fails to place an indicator it sets `WaitingForInput = true` and that move is not saved ([CC-021](../roadmap.md#cc-021)).

The file is deleted when `GameOver` becomes true (set by `GameEndConditionSystem`, which also sets `WaitingForInput = true`). `LevelComplete` is never set by any system ([CC-019](../roadmap.md#cc-019)), so that branch is never taken. Starting a new game, restarting, or returning to the menu does not delete the file: a save survives until the next saved move overwrites it or the game ends. Leaving a game from the footer "Main Menu" button leaves the last post-move save in place, and the menu then offers Resume.

The code comment "Detect if input was processed this frame ... transition from WaitingForInput=false to true" describes a transition the code does not detect; the test is simply `!WaitingForInput`.

### Resume hand-off
1. **Menu.** `MainMenuController` shows the resume button only if `SaveGameManager.HasSaveGame()` (checked in `SetupUI` and again in `OnEnable`). `ResumeGame` calls `LoadSaveGame()`; on success it sets `PlayerPrefs` key `LoadFromSave` = `"true"`, calls `PlayerPrefs.Save()` and loads the gameplay scene. On failure it logs `"Failed to load save game"` and stays on the menu (`Assets/Scripts/Gameplay/MainMenuController.cs`, `ResumeGame`).
2. **Gameplay scene.** `GameRenderer.InitializeGame` reads `LoadFromSave` (default `"false"`). If it is `"true"` it loads the save a second time, deletes the key (`DeleteKey` without `Save()`), and if the save loaded calls `InitializeFromSave`, then takes the updated configuration from `GameManager.ActiveConfiguration`. If the second load returns null it falls back to a normal new game (`Assets/Scripts/Gameplay/GameRenderer.cs`, `InitializeGame`).
3. **State.** `InitializeGame` creates the singletons as for a new game, builds the visual grid, then calls `RestoreWorldState(saveData)` instead of `SpawnInitialBlock`. The first frame after resume replays the resolve/spawn step ([CC-022](../roadmap.md#cc-022)).

### Versioning and migration
There is none. `version` is written as `"1.0"` and never read; there is no format check, upgrade path or handling of unknown/missing fields beyond what `JsonUtility` does (missing fields take their type defaults; a field added in a later version will read as default from an old file).

### Error handling
- Load: exceptions caught, logged with `Debug.LogError`, `null` returned. The menu and `InitializeGame` both handle `null`.
- Save and delete: wrapped in `try/catch`; failures are logged with `Debug.LogError` and the game continues.
- If `GameManager.ActiveConfiguration` or `ActiveColorProfile` is null when saving, the corresponding fields are simply left at their defaults (mode `Standard`, size 0, empty name); the save is still written (`SaveGameSystem.CreateSaveData`). Unverified whether this can occur in practice.
- Debug log lines for successful save, load and delete are compiled for the editor only (`#if UNITY_EDITOR`), except the `"Save game deleted"` log in `SaveGameManager.DeleteSaveGame`.

## Interactions with other systems
- Frame order and the one-save-per-move behaviour: [ecs-frame-order.md](ecs-frame-order.md).
- Configuration, `GameManager` and the start-up path that calls `InitializeFromSave`/`RestoreWorldState`: [configuration.md](configuration.md).
- Menu UI that shows the resume button: [ui-layout.md](ui-layout.md).
- Restored blocks are drawn like any other block: [presentation.md](presentation.md).
- Platform cloud saves are future work: [CC-013](../roadmap.md#cc-013).

## Known issues
- Resume replays the resolve/spawn step, and restored blocks (including the indicator) get a disabled `MovingBlockTag`. [CC-022](../roadmap.md#cc-022)
- `SaveGameSystem` calls its own `DeleteSaveGame` (a `File.Exists` check) every frame while `GameOver` is true; `SaveGameManager.DeleteSaveGame` is unused and the file path is duplicated in two places. [CC-024](../roadmap.md#cc-024)
- A move whose indicator spawn fails is not saved. [CC-021](../roadmap.md#cc-021)
- The `LevelComplete` delete branch is unreachable. [CC-019](../roadmap.md#cc-019)
- The stored RNG state is not advanced by colour picks, so resuming reproduces the same next roll. [CC-020](../roadmap.md#cc-020)
- The resumed configuration replaces the shared static runtime configuration. [CC-027](../roadmap.md#cc-027)
- Cloud saves and sync do not exist yet. [CC-013](../roadmap.md#cc-013)
- `version` and `saveTimestamp` are written but never read, so there is no way to reject or migrate an incompatible save file (`Assets/Scripts/SaveSystem/SaveData.cs`, `Assets/Scripts/SaveSystem/SaveGameManager.cs`). [CC-031](../roadmap.md#cc-031)
- the save file is written in place with one `File.WriteAllText` (no temp-file-and-rename), so a crash or kill during the write can leave a truncated file; `HasSaveGame` would then show the resume button and `LoadSaveGame` would return null or default-filled data (`Assets/Scripts/ECS/Systems/SaveGameSystem.cs`, `SaveGame`). [CC-031](../roadmap.md#cc-031)
- `colorProfileName` and `currentFrameScore` are saved but ignored on restore (`Assets/Scripts/SaveSystem/SaveData.cs`); a save made with a different colour profile resumes with the current one. [CC-031](../roadmap.md#cc-031)

## Related test cases
- [Save/Resume](../test-plan.md#saveresume-save): TP-SAVE-01 to TP-SAVE-06.
- [Game over](../test-plan.md#game-over-over): TP-OVER-06 (save deleted on game over).
