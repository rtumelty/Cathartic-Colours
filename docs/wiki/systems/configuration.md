# Configuration and game start

## Purpose
Describes the two ScriptableObject types that configure a game (`GameConfiguration`, `ColorProfile`), the asset instances in the project, the static `GameManager` that holds the runtime copies, and the path from the main menu to a running game (new game, restart and resume). The rules those settings feed are in [merge-rules.md](merge-rules.md); the ECS entities that start-up creates are in [ecs-frame-order.md](ecs-frame-order.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Data/GameConfiguration.cs` | Game rules/size/scoring settings (ScriptableObject) |
| `Assets/Scripts/Data/ColorProfile.cs` | Colours and the in-game UI tree (ScriptableObject) |
| `Assets/Scripts/Core/GameManager.cs` | Static holder of the runtime configuration copies (Core assembly) |
| `Assets/Scripts/Gameplay/PresentationRegistry.cs` | Static holder of the scene's presentation objects (Presentation assembly, since CC-002) |
| `Assets/Scripts/Gameplay/MainMenuController.cs` | Menu UI, edits the runtime configuration, starts or resumes |
| `Assets/Scripts/Gameplay/GameRenderer.cs` | Reads the configuration and creates the ECS state (`InitializeGame`) |
| `Assets/Scripts/SaveSystem/SaveGameManager.cs` | `InitializeFromSave`, `RestoreWorldState` (resume entry points) |
| `Assets/Data/Game Configs/*.asset`, `Assets/Data/Color Profiles/DefaultColorProfile.asset` | The asset instances |
| `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Gameplay scene.unity`, `Assets/Prefabs/GameManager.prefab` | Scene and prefab wiring |

## How it works

### GameConfiguration
`[CreateAssetMenu(fileName = "GameConfiguration", menuName = "CatharticColors/GameConfiguration")]` (`Assets/Scripts/Data/GameConfiguration.cs`).

| Field | Type | Default | Meaning |
|---|---|---|---|
| `gameMode` | `GameMode` (`Standard`, `ColorMerge`, `AdvancedColorMerge`) | `Standard` | Selects the merge rule struct through `MergeRulesFactory` ([merge-rules.md](merge-rules.md)), via the `GameModeComponent` |
| `gridWidth` | int | 6 | Grid columns |
| `gridHeight` | int | 6 | Grid rows |
| `spawnNextColourSystem` | bool | true | Copied into `ScoringConfigComponent.Settings.SpawnEnabled` by `GameRenderer.InitializeGame`; when true, each move spawns the next indicator |
| `tier1Points` | int | 10 | Points for Tier1 merges |
| `tier2Points` | int | 50 | Tier2 |
| `tier3Points` | int | 100 | Tier3 |
| `tier4Points` | int | 200 | Tier4 |

`GetPointsForTier(ScoreTier)` maps a tier to those values (0 for `None`). Since CC-006.3 the game scores through `ScoringConfigComponent` (copied from these fields at game start) instead, and `GetPointsForTier` has no callers.

### ColorProfile
`[CreateAssetMenu(fileName = "ColorProfile", menuName = "CatharticColors/ColorProfile")]` (`Assets/Scripts/Data/ColorProfile.cs`).

| Field | Type | Default | Meaning |
|---|---|---|---|
| `backgroundColor` | `Color` | light gray | Camera background (`GameRenderer.SetupUI`) |
| `gridBackgroundColor` | `Color` | dark gray | Intended tint of the grid cells; **not applied**, because `GameRenderer.CreateVisualGrid` looks for a `SpriteRenderer` and the `Cell` prefab has only a `MeshRenderer` ([CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41), [presentation](presentation.md)) |
| `uiTreeAsset` | `VisualTreeAsset` | none | In-game UI document assigned by `GameUIManager.SetupUI` |
| `colorMappings` | `BlockColorMapping[]` (`blockColor`, `renderColor`) | one entry per `BlockColor`: None clear, Red, Green, Blue, Yellow, Magenta, Cyan, White (Unity named colours) | `GetColor(BlockColor)`; logs a warning and returns white if a colour has no mapping. `OnValidate` warns about missing entries |

Public accessors: `BackgroundColor`, `GridBackgroundColor`, `UITreeAsset`, `GetColor`.

### Asset instances
Found by matching the `.cs.meta` guids of the two scripts against `m_Script` in `.asset` files.

| Asset | Type | Key values |
|---|---|---|
| `Assets/Data/Game Configs/Default.asset` | GameConfiguration | Standard, 6x6, spawn on |
| `Assets/Data/Game Configs/Standard_6x6.asset` | GameConfiguration | Standard, 6x6, spawn on |
| `Assets/Data/Game Configs/ColorMerge_4x4.asset` | GameConfiguration | Color Merge, 4x4, spawn on |
| `Assets/Data/Game Configs/AdvancedColorMerge_6x6.asset` | GameConfiguration | Advanced Color Merge, 6x6, spawn on |
| `Assets/Data/Color Profiles/DefaultColorProfile.asset` | ColorProfile | background (0.827, 0.827, 0.827), grid background (0.663, 0.663, 0.663), `uiTreeAsset` = `Assets/UI/GameUI.uxml`; Red (1,0,0), Green (0,1,0), Blue (0,0,1), Yellow (1, 0.922, 0.016), Magenta (1,0,1), Cyan (0,1,1), White (1,1,1), None transparent |

None of the four configuration assets serializes the `tier*Points` fields, so they take the field defaults (10 / 50 / 100 / 200).

Wiring:
- `MainMenu.unity`: `MainMenuController.defaultConfiguration` = `Default.asset`, `defaultColorProfile` = `DefaultColorProfile.asset`, `presetConfigurations` = AdvancedColorMerge_6x6, ColorMerge_4x4, Standard_6x6 (in that order), `gameSceneName` = "Gameplay scene" (matches `Assets/Scenes/Gameplay scene.unity`; the field's code default is the same since CC-025).
- `Assets/Prefabs/GameManager.prefab` (carries `GameRenderer`): `fallbackConfiguration` = `Default.asset`, `fallbackColorProfile` = `DefaultColorProfile.asset`.
- Build settings list `Assets/Scenes/MainMenu.unity` then `Assets/Scenes/Gameplay scene.unity` (`ProjectSettings/EditorBuildSettings.asset`).

### Static GameManager
`Assets/Scripts/Core/GameManager.cs` (global namespace, `static class`, `CatharticColours.Core` assembly). Since CC-002 the four scene references live in `PresentationRegistry` (`Assets/Scripts/Gameplay/PresentationRegistry.cs`, global namespace, `CatharticColours.Presentation` assembly), so Core never references presentation types ([ADR-004](../decisions/ADR-004-core-presentation-assemblies.md)).

| Member | Holds | Set by |
|---|---|---|
| `ActiveConfiguration` | runtime **copy** of a `GameConfiguration` | `Initialize` |
| `ActiveColorProfile` | runtime **copy** of a `ColorProfile` | `Initialize` |
| `PresentationRegistry.ActiveGameRenderer` | `GameRenderer` | `GameRenderer.Awake` (nulled in `OnDestroy`) |
| `PresentationRegistry.ActiveGameUIManager` | `GameUIManager` | `GameUIManager.Awake` (nulled in `OnDestroy`) |
| `PresentationRegistry.ActivePointsParticleSpawner` | `PointsParticleSpawner` | that component (nulled on teardown) |
| `PresentationRegistry.JellyManager` | `JellyManager` | that component (nulled on teardown) |

- `Initialize(defaultConfig, defaultColorProfile)` logs an error and returns, leaving the current state unchanged, if the config or the colour profile is null (both are checked before either is replaced, CC-025); otherwise it stores `Object.Instantiate` copies so edits never touch the asset files.
- The two `Active*` getters log a warning when not initialized and return null. `IsInitialized` is `_activeConfiguration != null`.
- `ResetStatics` (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) clears every field on play-mode entry (each of the two classes has its own), to support Fast Enter Play Mode.
- Because it is static, the runtime configuration survives scene loads: returning to the main menu keeps the last mode, size and so on.

### Starting a game
1. **Main menu load.** `MainMenuController.Awake` calls `GameManager.Initialize(defaultConfiguration, defaultColorProfile)` only if `!IsInitialized`, then `SetupUI`, which queries the menu elements by name (`preset-dropdown`, `gamemode-dropdown`, `gamemode-description`, `width-slider`, `height-slider`, `width-value`, `height-value`, `start-button`, `resume-game-button`) and shows the current runtime values. The resume button is shown only if `SaveGameManager.HasSaveGame()`.
2. **Choosing mode and size.** All choices write straight into the runtime `ActiveConfiguration`:
   - mode dropdown ("Standard", "Color Merge", "Advanced Color Merge") sets `gameMode`;
   - sliders set `gridWidth` and `gridHeight` (range 4 to 10 in `Assets/UI/MainMenu.uxml`);
   - the preset dropdown ("Custom" plus each preset asset's name) calls `LoadPreset`, which copies only `gameMode`, `gridWidth`, `gridHeight` and `spawnNextColourSystem` from the preset into the runtime copy.
   The menu does not expose `spawnNextColourSystem`, the tier points or the colour profile.
3. **New game.** `StartGame` calls `SceneManager.LoadScene(gameSceneName)`.
4. **Gameplay scene.** `GameRenderer.Awake` registers itself in `GameManager` and calls `LoadConfigs`, which initializes `GameManager` from the prefab's fallback assets if the menu was skipped (for example when running the gameplay scene directly in the editor). `GameRenderer.Start` calls `SetupUI` (camera background from the profile, then `GameUIManager.SetupUI`, which assigns `ActiveColorProfile.UITreeAsset` to its `UIDocument`) and `InitializeGame`.
5. **`GameRenderer.InitializeGame`** reads `gridWidth`/`gridHeight`, then creates the ECS state: `GridConfigComponent`, `GameStateComponent` (`WaitingForInput = true`, random seed from `DateTime.Now.Ticks`), `ScoreComponent`, `GameModeComponent` (from `gameMode`) and `ScoringConfigComponent` (tier points and `spawnNextColourSystem`, as unmanaged data for `BoardStepSystem`, since CC-006.3); then the visual grid cells, then either the initial indicator block or the restored save. Details of the entities are in [ecs-frame-order.md](ecs-frame-order.md).
6. **Restart.** The game-over Restart button raises `GameUIManager.OnRestartRequested` (twice per click: `root.Q("restart-button")` and the game-over panel query find the same button, see [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40)); there is no restart button in the header; `GameRenderer.RestartGame` runs `CleanUp` then `InitializeGame` with the same runtime configuration. "Next level" also calls `RestartGame` (its handler is a TODO).

### Resume from save (entry point only)
- `MainMenuController.ResumeGame` loads the save file; if it loads, it sets `PlayerPrefs` key `LoadFromSave` to "true" and loads the gameplay scene.
- In `GameRenderer.InitializeGame`, if that key is "true" it deletes the key, loads the save again, and calls `SaveGameManager.InitializeFromSave(saveData, config, ActiveColorProfile)`, which replaces the `GameManager` configuration with a new instance (template from the current runtime copy, then `gameMode`, `gridWidth`, `gridHeight`, `spawnNextColourSystem` from the save) and re-instantiates the colour profile. Entity creation then proceeds as for a new game and `SaveGameManager.RestoreWorldState` overwrites game state, score and blocks instead of `SpawnInitialBlock`.
- If the save cannot be loaded the game starts as a new one. Save format and triggers: [save-system.md](save-system.md).

## Interactions with other systems
- Merge rules, tier points and spawn toggle: [merge-rules.md](merge-rules.md).
- Entities, tags and systems created from the configuration: [ecs-frame-order.md](ecs-frame-order.md).
- `GameUIManager` uses the profile's UI tree ([ui-layout.md](ui-layout.md)); `PointsParticleSpawner` and `GameRenderer` use the profile's colours ([presentation.md](presentation.md)); resume restores configuration ([save-system.md](save-system.md)).

## Known issues
- `LoadPreset` copies mode, size and spawn flag but not tier points, so presets cannot change scoring; the menu never exposes tier points or `spawnNextColourSystem` (`Assets/Scripts/Gameplay/MainMenuController.cs`, `LoadPreset`). [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39)
- Menu edits mutate the shared static runtime copy immediately, and `InitializeFromSave` replaces it; a resumed game's configuration therefore becomes the menu's current configuration after returning to the menu (`Assets/Scripts/Core/GameManager.cs`, `Assets/Scripts/SaveSystem/SaveGameManager.cs`). [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39)

## Related test cases
- [Main menu](../test-plan.md#main-menu-menu): TP-MENU-01 to TP-MENU-06 (defaults, presets, sliders, start).
- [Game over](../test-plan.md#game-over-over): TP-OVER-04, TP-OVER-05 (restart, duplicate cells CC-029). [Save/Resume](../test-plan.md#saveresume-save): TP-SAVE-02 (resume restores the saved configuration).
