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
- Path: `Path.Combine(Application.persistentDataPath, "savegame.json")`. Since CC-002.3 the path has one owner, `SaveGameManager.SaveFilePath`, which `SaveGameSystem` also uses; tests redirect it with the internal `SaveFilePathOverride` (`Assets/Scripts/SaveSystem/SaveGameManager.cs`, `Assets/Scripts/ECS/Systems/SaveGameSystem.cs`; see [assemblies](assemblies.md)).
- Format: `JsonUtility.ToJson(saveData, true)` (pretty-printed); read with `File.ReadAllText` and `JsonUtility.FromJson<SaveGameData>` (`SaveGameManager.WriteSave`, `SaveGameManager.LoadSaveGame`). Enums (`GameMode`, `BlockColor`, `BlockSize`) are serialised by `JsonUtility` as integers.
- **Atomic write** (since CC-031): `SaveGameManager.WriteSave` creates the directory if missing, writes the JSON to `savegame.json.tmp`, then moves it over `savegame.json` with `File.Replace` (or `File.Move` when there is no save yet). A crash mid-write leaves at worst a stale `.tmp` (overwritten by the next save, removed by `DeleteSaveGame`), never a truncated save. Where `File.Replace` is not supported the old file is deleted and the temp file moved, so a crash between the two leaves no save rather than a broken one.
- **Unusable saves** (since CC-031): a save that can't be parsed, is empty, has an unknown `version` or a grid smaller than 1x1 is moved to `savegame.bad.json` in the same folder (replacing an earlier one) with a `Debug.LogWarning`, and is treated as no save: no Resume button. Rotating backups and reporting are [CC-042](https://github.com/rtumelty/Cathartic-Colours/issues/70).

### Schema (`SaveGameData`)
`Assets/Scripts/SaveSystem/SaveData.cs`. "Written from" is `SaveGameSystem.CreateSaveData`; "On restore" is what `SaveGameManager` does with it.

| Field | Type | Written from | On restore |
|---|---|---|---|
| `version` | string | `SaveGameManager.CurrentVersion` (`"1.0"`) | Checked on load: only versions in `ReadableVersions` load; others are set aside (since CC-031) |
| `saveTimestamp` | string | constructor: `DateTime.Now.ToBinary().ToString()` | **Ignored** |
| `gameMode` | `GameMode` | `GameManager.ActiveConfiguration.gameMode` | `InitializeFromSave` copies it into a new runtime `GameConfiguration` |
| `gridWidth` | int | `ActiveConfiguration.gridWidth` | Same (`InitializeFromSave`) |
| `gridHeight` | int | `ActiveConfiguration.gridHeight` | Same |
| `spawnNextColourSystem` | bool | `ActiveConfiguration.spawnNextColourSystem` | Same |
| `colorProfileName` | string | the active profile's asset name, without the runtime copy's "(Clone)" suffix (`SaveGameManager.ProfileAssetName`, since CC-031) | `InitializeFromSave` uses the known profile with that name, else the template (since CC-031). Older saves with the suffix still match |
| `waitingForInput` | bool | always `true` since CC-006.5: the save describes the idle state after the move | Ignored: `RestoreWorldState` always sets `WaitingForInput = true`, so files written by older builds (which stored `false`) resume the same way |
| `gameOver` | bool | `GameStateComponent.GameOver` | Written back |
| `levelComplete` | bool | `GameStateComponent.LevelComplete` | Written back |
| `moveCount` | int | `GameStateComponent.MoveCount` | Written back |
| `randomState` | uint | `GameStateComponent.Random.state` | Restored directly (`new Random { state = randomState }`, since CC-044), so a resumed game continues the same random sequence; a zero state, which `Random` rejects, falls back to a fixed seed |
| `totalScore` | int | `ScoreComponent.TotalScore` | Written back |
| `currentFrameScore` | int | `ScoreComponent.CurrentFrameScore` | Written back into `ScoreComponent`, but nothing reads it later (`BoardStepSystem` resets it every frame) |
| `blocks` | `SavedBlock[]` | every entity with `BlockComponent` (`QueryBuilder().WithAll<BlockComponent>()`) | `RestoreBlocks` creates one entity per element |

`SavedBlock` (struct): `color` (`BlockColor`), `size` (`BlockSize`), `gridX`, `gridY` (ints), `isNextColorIndicator` (bool). All five are restored into `BlockComponent`.

Not saved: tier points and any other `GameConfiguration` fields beyond the four above (they come from the current runtime copy on resume), pending event entities, the pending `MoveDirectionComponent`, and `GameStateComponent.LastMoveDirection` (presentation only).

### `SaveGameManager`
`Assets/Scripts/SaveSystem/SaveGameManager.cs` (`static class`, namespace `SaveSystem`).

| Method | Behaviour |
|---|---|
| `HasSaveGame()` | True only when the file exists **and** passes the load checks; an unusable file is set aside here (since CC-031) |
| `LoadSaveGame()` | Returns `null` (with a warning) if the file is absent. Otherwise reads, deserialises and checks it (version, grid size); an unusable file is moved to `savegame.bad.json` with a warning and `null` is returned |
| `WriteSave(saveData)` (internal) | Sets `version` to `CurrentVersion` and writes atomically through `savegame.json.tmp` (see File location and format). Called by `SaveGameSystem` |
| `DeleteSaveGame()` | Deletes the save and any leftover `.tmp` if present. `SaveGameSystem` calls it on game over (since CC-031) |
| `InitializeFromSave(saveData, configTemplate, colorProfileTemplate, knownColorProfiles = null)` | Logs an error and returns if `saveData` is null. Otherwise instantiates `configTemplate`, overwrites `gameMode`, `gridWidth`, `gridHeight`, `spawnNextColourSystem` from the save, and calls `GameManager.Initialize` (which instantiates both again) with the profile from `knownColorProfiles` whose asset name matches `colorProfileName`, or else `colorProfileTemplate`. `GameRenderer` passes the current profile and its fallback profile; the game has one profile today (`DefaultColorProfile`) |
| `RestoreWorldState(saveData)` | Returns if `saveData` is null. Using `World.DefaultGameObjectInjectionWorld`: if a `GameStateComponent` entity exists, sets `WaitingForInput = true` and overwrites `GameOver`, `LevelComplete`, `MoveCount`, `Random` (state restored directly via `RestoreRandom`, CC-044) from the save; if a `ScoreComponent` entity exists, replaces it with the saved totals; then calls `RestoreBlocks` |
| `RestoreBlocks` (private) | Returns if the array is null or empty. Creates one entity per saved block with only a `BlockComponent` and sets the data, the same shape as blocks in a fresh game (since CC-006.3; before that, restored blocks got a `MovingBlockTag`, which let a restored indicator slide on the first move) |

Beyond the load checks (version, grid size), restored data is not validated: grid positions outside the saved width/height, overlapping blocks, or a `blocks` array of an unexpected size are all accepted as-is.

### Trigger points (`SaveGameSystem`)
`Assets/Scripts/ECS/Systems/SaveGameSystem.cs`: `[UpdateInGroup(PresentationSystemGroup)]`, `[UpdateAfter(MergeEffectsSystem)]`, `RequireForUpdate<GameStateComponent>`. Each frame `OnUpdate` reads `GameStateComponent` and:

| Condition | Action |
|---|---|
| `!WaitingForInput && !GameOver && !LevelComplete` | `SaveGame` (write the file) |
| else if `GameOver || LevelComplete`, and it was not so last frame | `DeleteSaveGame` (private; calls `SaveGameManager.DeleteSaveGame`, `try/catch` with `Debug.LogError`). Runs once when the game ends, not every frame (field `endedLastFrame`, since CC-006.3) |
| otherwise (idle: `WaitingForInput` and not over) | nothing |

In practice (see the frame walk-through in [ecs-frame-order.md](ecs-frame-order.md#one-player-move-frame-by-frame)): `WaitingForInput` is false only in the one frame in which `BoardStepSystem` accepted a move, because `EndFrameSystem` (last in the Presentation group, after `SaveGameSystem`) sets it back to true. So the file is written **once per accepted move**, after the move, merges, scoring, indicator resolve and new indicator spawn have all happened in that frame.

The file is deleted when `GameOver` becomes true (set by `BoardStepSystem`, which also sets `WaitingForInput = true`). `LevelComplete` is never set by any system ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)), so that branch is never taken. Starting a new game, restarting, or returning to the menu does not delete the file: a save survives until the next saved move overwrites it or the game ends. Leaving a game from the footer "Main Menu" button leaves the last post-move save in place, and the menu then offers Resume.

The test is simply `!WaitingForInput`, which `BoardStepSystem` sets only in the frame of an accepted move. The file it writes still records `waitingForInput = true`: the save describes the idle state after the move (since CC-006.5).

### Resume hand-off
1. **Menu.** `MainMenuController` shows the resume button only if `SaveGameManager.HasSaveGame()` (checked in `SetupUI` and again in `OnEnable`). `ResumeGame` calls `LoadSaveGame()`; on success it sets `PlayerPrefs` key `LoadFromSave` = `"true"`, calls `PlayerPrefs.Save()` and loads the gameplay scene. On failure it logs `"Failed to load save game"` and stays on the menu (`Assets/Scripts/Gameplay/MainMenuController.cs`, `ResumeGame`).
2. **Gameplay scene.** `GameRenderer.InitializeGame` reads `LoadFromSave` (default `"false"`). If it is `"true"` it loads the save a second time, deletes the key (`DeleteKey` without `Save()`), and if the save loaded calls `InitializeFromSave`, then takes the updated configuration from `GameManager.ActiveConfiguration`. If the second load returns null it falls back to a normal new game (`Assets/Scripts/Gameplay/GameRenderer.cs`, `InitializeGame`).
3. **State.** `InitializeGame` creates the singletons as for a new game, builds the visual grid, then calls `RestoreWorldState(saveData)` instead of `SpawnInitialBlock`. The game resumes waiting for input: nothing runs until the first move, so the board, the white indicator, Moves and Score stay exactly as saved, and the file is not rewritten (fixed in CC-006.5, [CC-022](https://github.com/rtumelty/Cathartic-Colours/issues/34)).

### Versioning and migration
Since CC-031 `version` is checked: `SaveGameManager.CurrentVersion` (`"1.0"`) is written, and a load accepts only versions listed in `ReadableVersions`; anything else is set aside as `savegame.bad.json`. There is no migration yet. When the format changes, bump `CurrentVersion` and keep an old version in `ReadableVersions` only if it can still be read (with a migration step if fields change meaning). Missing fields take their type defaults with `JsonUtility`. `saveTimestamp` is still written and not read.

### Error handling
- Load: an unreadable or unknown save is set aside with `Debug.LogWarning` and `null` is returned. The menu and `InitializeGame` both handle `null`.
- Save and delete: wrapped in `try/catch`; failures are logged with `Debug.LogError` and the game continues.
- If `GameManager.ActiveConfiguration` or `ActiveColorProfile` is null when saving, the corresponding fields are simply left at their defaults (mode `Standard`, size 0, empty name); the save is still written (`SaveGameSystem.CreateSaveData`). Unverified whether this can occur in practice.
- The save system logs only warnings and errors: a missing file on load or an unusable save (`Debug.LogWarning`) and failures (`Debug.LogError`). Successful save, load and delete log nothing.

## Interactions with other systems
- Frame order and the one-save-per-move behaviour: [ecs-frame-order.md](ecs-frame-order.md).
- Configuration, `GameManager` and the start-up path that calls `InitializeFromSave`/`RestoreWorldState`: [configuration.md](configuration.md).
- Menu UI that shows the resume button: [ui-layout.md](ui-layout.md).
- Restored blocks are drawn like any other block: [presentation.md](presentation.md).
- Platform cloud saves are future work: [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25).

## Known issues
- The `LevelComplete` delete branch is unreachable. [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)
- The resumed configuration replaces the shared static runtime configuration. [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39)
- Cloud saves and sync do not exist yet. [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25)
- `currentFrameScore` is restored, but nothing reads it.

## Related test cases
- [Save/Resume](../test-plan.md#saveresume-save): TP-SAVE-01 to TP-SAVE-06.
- [Game over](../test-plan.md#game-over-over): TP-OVER-06 (save deleted on game over).
