# Save system

## Purpose
Describes how a game in progress is written to disk after each move, how the main menu offers to resume it, and how the saved state is turned back into configuration, singletons and block entities. There is one game slot. Since CC-042 it is kept as three JSON files: the newest save and the two moves before it, rotated after every move and all deleted when the game ends. A load falls back to the newest file that is usable.

All paths are relative to the Unity project folder (`Cathartic Colours/`). Frame ordering of the systems involved is in [ecs-frame-order.md](ecs-frame-order.md); the start-up path that calls the restore code is in [configuration.md](configuration.md).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/SaveSystem/SaveData.cs` | `SaveGameData` and `SavedBlock` (the serialised schema) |
| `Assets/Scripts/SaveSystem/SaveGameManager.cs` | Also holds `SaveRejectReason`, `SaveRejection` and the `SaveRejected` event. Static helper: `HasSaveGame`, `LoadSaveGame`, `DeleteSaveGame`, `InitializeFromSave` (starts a resumed game through `GameManager.StartResumedGame` without touching the menu's configuration), `RestoreWorldState`, `RestoreBlocks` |
| `Assets/Scripts/ECS/Systems/SaveGameSystem.cs` | Writes and deletes the save file (Presentation group) |
| `Assets/Scripts/Gameplay/MainMenuController.cs` | Shows the resume button, sets the `LoadFromSave` PlayerPrefs flag (`ResumeGame`) |
| `Assets/Scripts/Gameplay/GameRenderer.cs` | Reads the flag and calls the restore code (`InitializeGame`) |

## How it works

### File location and format
- Path: `Path.Combine(Application.persistentDataPath, "savegame.json")`. Since CC-002.3 the path has one owner, `SaveGameManager.SaveFilePath`, which `SaveGameSystem` also uses; tests redirect it with the internal `SaveFilePathOverride` (`Assets/Scripts/SaveSystem/SaveGameManager.cs`, `Assets/Scripts/ECS/Systems/SaveGameSystem.cs`; see [assemblies](assemblies.md)).
- Format: `JsonUtility.ToJson(saveData, true)` (pretty-printed); read with `File.ReadAllText` and `JsonUtility.FromJson<SaveGameData>` (`SaveGameManager.WriteSave`, `SaveGameManager.LoadSaveGame`). Enums (`GameMode`, `BlockColor`, `BlockSize`) are serialised by `JsonUtility` as integers.
- **Atomic write** (since CC-031): `SaveGameManager.WriteSave` creates the directory if missing, writes the JSON to `savegame.json.tmp`, then moves it over `savegame.json` with `File.Replace` (or `File.Move` when there is no save yet). A crash mid-write leaves at worst a stale `.tmp` (overwritten by the next save, removed by `DeleteSaveGame`), never a truncated save. Where `File.Replace` is not supported the old file is deleted and the temp file moved, so a crash between the two leaves no save rather than a broken one.
- **Rotating backups** (since CC-042, `SaveGameManager.WriteSave`): the files are `savegame.json` (newest, the name older builds used, so their saves still load), `savegame.1.json` (the move before) and `savegame.2.json` (the move before that). `SaveGameManager.SaveSlotPath(slot)` gives slot 0, 1 or 2; `BackupCount` is 2. On each save, after the temp file is written: delete `.2`, move `.1` to `.2`, then `File.Replace(tmp, savegame.json, savegame.1.json)`, which moves the old newest file into `.1` in the same call. Where `File.Replace` is not supported, `savegame.json` is moved to `.1` and the temp file moved in. A crash at any step leaves at least one complete earlier save.
- **New game:** saves happen only after a move, so a save with `moveCount` 1 is a new game's first save. It deletes `.1` and `.2` and replaces `savegame.json` without keeping it, so a fallback never loads the previous game.
- **Fallback on load** (since CC-042, `TryReadSave`): `HasSaveGame` and `LoadSaveGame` read slot 0, then 1, then 2, and use the first file that passes the checks. Each unusable file on the way is set aside. When a backup is used, the files are renumbered (`PromoteSlot`) so it becomes `savegame.json` and the next save rotates normally. A resume that fell back looks like a normal resume, one or two moves earlier; there is no player message (decision D5 on the [CC-042 page](../features/CC-042-rotating-saves.md)).
- **Unusable saves** (since CC-031): a file that can't be parsed, is empty, has an unknown `version` or a grid smaller than 1x1 is moved to `savegame.bad.<UTC yyyyMMdd'T'HHmmssfff'Z'>.json` in the same folder (since CC-042.2; one file per rejection, and only the newest three are kept) with one `Debug.LogWarning`, and the next slot is tried. With no usable file there is no Resume button. See Reporting below.
- **Test seams:** `SaveFilePathOverride` (the folder) and `SimulateNoFileReplace` (forces the no-`File.Replace` path), both internal.
- **Cloud saves ([CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25)):** sync only the newest valid save (`LoadSaveGame`'s result); the backups stay on the device.

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
| `HasSaveGame()` | True when the newest save or a backup passes the load checks (fallback since CC-042); unusable files are set aside and a used backup is renumbered to `savegame.json` here |
| `LoadSaveGame()` | Returns `null` with the warning "No save game file found" if none of the three files exists. Otherwise returns the newest file that passes the checks (version, grid size), setting aside each unusable one and renumbering as `HasSaveGame` does; `null` when none passes |
| `WriteSave(saveData)` (internal) | Sets `version` to `CurrentVersion`, writes atomically through `savegame.json.tmp` and rotates the backups; a save with `moveCount` 1 clears them instead (see File location and format). Called by `SaveGameSystem` |
| `DeleteSaveGame()` | Deletes `savegame.json`, both backups and any leftover `.tmp`; set-aside bad files are kept. `SaveGameSystem` calls it on game over |
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

The file is deleted when `GameOver` becomes true (set by `BoardStepSystem`, which also sets `WaitingForInput = true`). `LevelComplete` is never set by any system ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)), so that branch is never taken. Starting a new game, restarting, or returning to the menu does not delete the files: a save survives until the next saved move replaces it (a new game's first move also clears the backups) or the game ends. Leaving a game from the footer "Main Menu" button leaves the last post-move save in place, and the menu then offers Resume.

The test is simply `!WaitingForInput`, which `BoardStepSystem` sets only in the frame of an accepted move. The file it writes still records `waitingForInput = true`: the save describes the idle state after the move (since CC-006.5).

### Resume hand-off
1. **Menu.** `MainMenuController` shows the resume button only if `SaveGameManager.HasSaveGame()` (checked in `SetupUI` and again in `OnEnable`). `ResumeGame` calls `LoadSaveGame()`; on success it sets `PlayerPrefs` key `LoadFromSave` = `"true"`, calls `PlayerPrefs.Save()` and loads the gameplay scene. On failure it logs `"Failed to load save game"` and stays on the menu (`Assets/Scripts/Gameplay/MainMenuController.cs`, `ResumeGame`).
2. **Gameplay scene.** `GameRenderer.InitializeGame` reads `LoadFromSave` (default `"false"`). If it is `"true"` it loads the save a second time, deletes the key (`DeleteKey` without `Save()`), and if the save loaded calls `InitializeFromSave`, then takes the updated configuration from `GameManager.ActiveConfiguration`. If the second load returns null it falls back to a normal new game (`Assets/Scripts/Gameplay/GameRenderer.cs`, `InitializeGame`).
3. **State.** `InitializeGame` creates the singletons as for a new game, builds the visual grid, then calls `RestoreWorldState(saveData)` instead of `SpawnInitialBlock`. The game resumes waiting for input: nothing runs until the first move, so the board, the white indicator, Moves and Score stay exactly as saved, and the file is not rewritten (fixed in CC-006.5, [CC-022](https://github.com/rtumelty/Cathartic-Colours/issues/34)).

### Versioning and migration
Since CC-031 `version` is checked: `SaveGameManager.CurrentVersion` (`"1.0"`) is written, and a load accepts only versions listed in `ReadableVersions`; anything else is set aside as a `savegame.bad.<timestamp>.json` file. There is no migration yet. When the format changes, bump `CurrentVersion` and keep an old version in `ReadableVersions` only if it can still be read (with a migration step if fields change meaning). Missing fields take their type defaults with `JsonUtility`. `saveTimestamp` is still written and not read.

### Reporting (since CC-042)
Each rejected file is reported once, in three ways: moved aside, logged and raised as an event. All are in `Assets/Scripts/SaveSystem/SaveGameManager.cs`.

- **Reason:** `SaveRejectReason` is `Unreadable` (the file could not be read or parsed; the detail is the exception message), `Empty` (the file parsed to nothing; detail is empty), `UnknownVersion` (detail is the version, for example `99.0`) or `InvalidGrid` (detail is the size, for example `0x4`).
- **Record:** `SaveRejection` is a read-only struct with `Reason`, `FileName` (the slot's file name, for example `savegame.1.json`), `Detail` and `SetAsidePath` (the full path of the bad file, or `null` if the move failed).
- **Event:** `SaveGameManager.SaveRejected` (`event Action<SaveRejection>`) is raised once per rejected file, after the move and the log line. It runs synchronously on the calling thread, which is the main thread, during `HasSaveGame` or `LoadSaveGame`. A subscriber that throws is logged with `Debug.LogException` and does not stop the load or the other subscribers. Nothing subscribes yet.
- **Log line:** exactly one warning per rejected file, built by `FormatRejection`: `Save rejected: reason=<Reason> file=<FileName> detail=<Detail> movedTo=<bad file name or none>`. If the move fails, the warning still shows `movedTo=none` and a `Debug.LogError` ("Could not move the rejected save aside: ...") follows.
- **Bad files:** named `savegame.bad.<UTC yyyyMMdd'T'HHmmssfff'Z'>.json`. If the name exists, 1 ms is added until it is free, so names are unique and sort in time order. After each move, all but the newest three (`KeptBadFiles`) are deleted. A `savegame.bad.json` left by an older build does not match the name pattern, so it is not counted or deleted. A failure while trimming is logged as a warning and does not stop the load.

**How CC-015 (analytics and crash reporting) should subscribe:**
- Subscribe once at start-up, before the main menu calls `HasSaveGame`, for example from a bootstrap. A later subscriber misses the rejections already raised.
- The handler runs inside the load, on the main thread. Keep it cheap and non-throwing: queue or send the event, do not block.
- Never read, move or delete save files from the handler. The load is still working through the slots.
- Send `Reason`, `FileName` and `Detail`. `Detail` for `Unreadable` is a parser message, so do not parse it.

### Error handling
- Load: an unreadable or unknown file is set aside with one `Debug.LogWarning` (and a `SaveRejected` event) and the next backup is tried; `null` is returned when none is usable. The menu and `InitializeGame` both handle `null`. If renumbering after a fallback fails, a `Debug.LogError` is logged and the usable file stays in its slot, where the next load finds it again.
- Save and delete: wrapped in `try/catch`; failures are logged with `Debug.LogError` and the game continues.
- If `GameManager.ActiveConfiguration` or `ActiveColorProfile` is null when saving, the corresponding fields are simply left at their defaults (mode `Standard`, size 0, empty name); the save is still written (`SaveGameSystem.CreateSaveData`). Unverified whether this can occur in practice.
- The save system logs only warnings and errors: a missing file on load or an unusable save (`Debug.LogWarning`) and failures (`Debug.LogError`). Successful save, load and delete log nothing.

## Cost
Measured on a development build on a Galaxy S23, 2026-11-21 (CC-042): a save takes about 2.8 ms median and 3.6 ms p95 on the main thread, once per move, almost all of it the write and rotation. Development device builds log a `SaveTiming` line per save (`adb logcat -s Unity`). On Android, `persistentDataPath` is `/sdcard/Android/data/com.catharticcolours.game/files/`, readable with `adb pull`.

## Interactions with other systems
- Frame order and the one-save-per-move behaviour: [ecs-frame-order.md](ecs-frame-order.md).
- Configuration, `GameManager` and the start-up path that calls `InitializeFromSave`/`RestoreWorldState`: [configuration.md](configuration.md).
- Menu UI that shows the resume button: [ui-layout.md](ui-layout.md).
- Restored blocks are drawn like any other block: [presentation.md](presentation.md).
- Platform cloud saves are future work: [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25).

## Known issues
- The `LevelComplete` delete branch is unreachable. [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)
- Cloud saves and sync do not exist yet. [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25)
- `currentFrameScore` is restored, but nothing reads it.

## Related test cases
- [Save/Resume](../test-plan.md#saveresume-save): TP-SAVE-01 to TP-SAVE-11 (TP-SAVE-10 and TP-SAVE-11: backups and fallback, CC-042).
- [Game over](../test-plan.md#game-over-over): TP-OVER-06 (save deleted on game over).
