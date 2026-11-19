# UI layout

## Purpose
Describes the UI Toolkit assets (UXML, USS, panel settings, theme), which scene or component uses each, every element name the C# code looks up and whether it exists in the UXML, how the game-over and level-complete panels are shown and hidden, the events `GameUIManager` raises and who subscribes, and how `LayoutObserver` measures the header and footer so `GameRenderer` can fit the board between them.

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/UI/MainMenu.uxml` | Main-menu layout |
| `Assets/UI/GameUI.uxml` | In-game layout (header, panels, footer) |
| `Assets/UI/GameUI.uss` | The only stylesheet; referenced by both UXML files |
| `Assets/UI/GamEPanelSettings.asset` | Shared `PanelSettings` asset (the file name has a capital E; the object name is `GamePanelSettings`) |
| `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` | Theme used by the panel settings (`@import url("unity-theme://default");`) |
| `Assets/Scripts/Gameplay/GameUIManager.cs` | In-game UI controller |
| `Assets/Scripts/Gameplay/MainMenuController.cs` | Main-menu UI controller |
| `Assets/Scripts/Gameplay/LayoutObserver.cs` | Measures header and footer |
| `Assets/Scripts/Gameplay/GameRenderer.cs` | Subscribes to UI events, `UpdateCamera` |
| `Assets/Data/Color Profiles/DefaultColorProfile.asset` | `uiTreeAsset` = `GameUI.uxml` |

## How it works

### Assets and what uses them
| Asset | Used by |
|---|---|
| `MainMenu.uxml` | `UIDocument` on the `MainMenuUI` object in `Assets/Scenes/MainMenu.unity` (`sourceAsset`), driven by `MainMenuController` |
| `GameUI.uxml` | `UIDocument` on the `GameUI` child of `Assets/Prefabs/GameManager.prefab` (`sourceAsset`), driven by `GameUIManager`. At runtime `GameUIManager.SetupUI` overwrites `visualTreeAsset` with `GameManager.ActiveColorProfile.UITreeAsset`, which in the default profile is the same `GameUI.uxml` (`Assets/Scripts/Gameplay/GameUIManager.cs`, `Assets/Data/Color Profiles/DefaultColorProfile.asset`) |
| `GameUI.uss` | Both UXML files include it through a `<Style src="project://database/Assets/UI/GameUI.uss ...">` element. It holds menu classes (`menu-*`, `slider-*`, `tertiary-button`) and game classes (`header`, `footer`, `modal-*`, `*-button`, `hint-text`, `instruction-text`) in one file (340 lines) |
| `GamEPanelSettings.asset` | Both `UIDocument` components. `ScaleMode` is 0 (ConstantPixelSize, scale 1), sort order 0, theme `UnityDefaultRuntimeTheme.tss`; the 1920 x 1280 reference resolution is stored but unused in this scale mode |
| `UnityDefaultRuntimeTheme.tss` | The panel settings' theme |

The gameplay scene (`Assets/Scenes/Gameplay scene.unity`) has no `UIDocument` of its own: the UI arrives through the `GameManager` prefab instance.

### Element names queried by the C#
"In UXML" refers to the UXML file that the controller's `UIDocument` shows. **Mismatches are in bold.**

`GameUIManager.CacheUIElements` (`GameUIManager.cs`) against `GameUI.uxml`:

| Query | Type | Field | In UXML | Notes |
|---|---|---|---|---|
| `Q<Label>("move-count")` | Label | `moveCountLabel` | yes | in `header` |
| `Q<Label>("score-label")` | Label | `scoreLabel` | yes | in `header` |
| `Q<Label>("status-label")` | Label | `statusLabel` | **NO** | Not in `GameUI.uxml` (only a `.status-label` style exists in the USS), so `statusLabel` is always null and the status text ("Your turn...", "Processing...", "Grid Full!", "Level Complete!") is never shown |
| `Query<Label>(className: "instruction-text").First()` | Label (by class) | `instructionLabel` | yes | The Label in the footer. A different element, a `VisualElement` *named* `instruction-text`, wraps it and is not queried |
| `Q<VisualElement>("game-over-panel")` | VisualElement | `gameOverPanel` | yes | |
| `Q<VisualElement>("level-complete-panel")` | VisualElement | `levelCompletePanel` | yes | |
| `Q<Button>("restart-button")` | Button | `restartButton` | yes, **twice** | One in each panel. `Q` returns the first in document order, the **game-over** panel's button |
| `levelCompletePanel?.Q<Button>("next-level-button")` | Button | `nextLevelButton` | yes | |
| `Q<Button>("footer-main-menu-button")` | Button | `footerMainMenuButton` | yes | in `footer` |

Looked up from inside the panels in `HookUpEvents` / `UnhookEvents`:

| Query | In UXML | Notes |
|---|---|---|
| `gameOverPanel?.Q<Button>("restart-button")` | yes | The same element as `restartButton`, so the game-over restart button gets its click handler **twice** (see Known issues) |
| `gameOverPanel?.Q<Button>("main-menu-button")` | yes | |
| `levelCompletePanel?.Q<Button>("main-menu-button")` | yes | |

The level-complete panel's own `restart-button` (the second one in `GameUI.uxml`) is never queried, so it has no handler.

`LayoutObserver.CalculateAndNotifyLayout` (`LayoutObserver.cs`):

| Query | Type | In `GameUI.uxml` |
|---|---|---|
| `Q<VisualElement>("header")` | VisualElement | yes |
| `Q<VisualElement>("footer")` | VisualElement | yes |

`MainMenuController.SetupUI` (`MainMenuController.cs`) against `MainMenu.uxml`:

| Query | Type | Field | In UXML | UXML element |
|---|---|---|---|---|
| `Q<DropdownField>("preset-dropdown")` | DropdownField | `presetDropdown` | yes | `DropdownField` |
| `Q<DropdownField>("gamemode-dropdown")` | DropdownField | `gameModeDropdown` | yes | `DropdownField` |
| `Q<Label>("gamemode-description")` | Label | `gameModeDescription` | yes | `Label` |
| `Q<SliderInt>("width-slider")` | SliderInt | `widthSlider` | yes | `SliderInt` (low 4, high 10) |
| `Q<SliderInt>("height-slider")` | SliderInt | `heightSlider` | yes | `SliderInt` (low 4, high 10) |
| `Q<Label>("width-value")` | Label | `widthValue` | yes | `Label` |
| `Q<Label>("height-value")` | Label | `heightValue` | yes | `Label` |
| `Q<Button>("start-button")` | Button | `startButton` | yes | `Button` |
| `Q<Button>("resume-game-button")` | Button | `resumeGameButton` | yes | `Button` |

All nine menu names match. No other script queries the UI (search of `Assets/Scripts` for `Q<`, `.Q(`, `Query`, `rootVisualElement`).

Elements in the UXML that no code queries: `root`, `header` (menu), `title`, `menu-content`, `controls-hint`, `instruction-text` (the container), `footer` (menu), the level-complete `restart-button`.

### Panels and how they are shown
- `GameUI.uxml` structure: `root` contains `header` (title, `move-count`, `score-label`), `game-over-panel`, `level-complete-panel`, then a wrapper holding `footer` (`instruction-text` container, `controls-hint`, `footer-main-menu-button`).
- Both panels use class `modal-panel` (`GameUI.uss`): `position: absolute`, full size, centred content, `rgba(0,0,0,0.8)` background, `display: none`.
- `InitializeUIState` sets both to `DisplayStyle.None` at set-up. After that `GameRenderer.Update` calls `UpdateUI`, which calls `GameUIManager.UpdateGameState(gameState, score)` **every frame** (`GameRenderer.cs`, `Update`/`UpdateUI`). That method sets `gameOverPanel.style.display` to `Flex` while `GameOver` is true and `None` otherwise, and the same for `levelCompletePanel` with `LevelComplete`.
- The game-over panel therefore appears the frame after `BoardStepSystem` sets `GameOver`, and disappears when a restart recreates the state. `LevelComplete` is never set by any system, so the level-complete panel never appears ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)).
- Panels sit above the board (they are UI overlay); they do not block keyboard or touch input ([input.md](input.md)).

### Text updates
All in `GameUIManager`:

| Element | Text | Updated |
|---|---|---|
| `move-count` | `Moves: {MoveCount}` | each frame (`UpdateGameState`) |
| `score-label` | `Score: {TotalScore}` | each frame |
| `status-label` | "Grid Full!" / "Level Complete!" / "Your turn..." / "Processing..." (in that precedence) | each frame, but the element does not exist in the UXML (above) |
| `instruction-text` label | One of three mode descriptions from `UpdateInstructionText(GameMode)` (Standard, Color Merge, Advanced Color Merge; fallback "Merge matching tiles") | once per `GameRenderer.InitializeGame` (new game or restart) |
| Static labels | Title, "Swipe / Arrow Keys / WASD to Move", modal titles and messages | baked into the UXML |

The in-game score shown is `ScoreComponent.TotalScore`; `CurrentFrameScore` is not displayed.

### Events raised by `GameUIManager`
| Event | Raised when | Subscriber |
|---|---|---|
| `OnRestartRequested` (`Action`) | A hooked restart button is clicked | `GameRenderer.RestartGame` (runs `CleanUp` then `InitializeGame`) |
| `OnNextLevelRequested` (`Action`) | `next-level-button` clicked (inside the level-complete panel, which can never show) | `GameRenderer.OnNextLevel` (TODO; calls `RestartGame`) |
| `OnLayoutRecalculated` (`Action<float, float>`) | Forwarded from `LayoutObserver.OnLayoutRecalculated` (`HandleLayoutRecalculated`) | `GameRenderer.UpdateCamera(headerHeight, footerHeight)` |

`GameRenderer.OnEnable` starts a coroutine that waits until `GameManager.ActiveGameUIManager` is non-null, then subscribes all three; `OnDisable` unsubscribes if the manager still exists (`GameRenderer.cs`). Main-menu buttons are not events: the three "main menu" buttons call `ReturnToMainMenu`, which loads the scene named by `mainMenuSceneName` (`"MainMenu"` in the prefab) directly.

### `LayoutObserver` and the camera
`Assets/Scripts/Gameplay/LayoutObserver.cs` (plain class, constructed by `GameUIManager.SetupUI` with the `UIDocument`):
1. In the constructor it schedules `RegisterLayoutCallback` after 100 ms.
2. That registers a `GeometryChangedEvent` callback on the document root (the first one is deferred one more tick) and also schedules an immediate calculation. Every later geometry change recalculates.
3. `CalculateAndNotifyLayout` finds `header` and `footer`:
   - `topPadding` = `header.worldBound.yMax` (if the header has positive height and `yMax > 0`);
   - `bottomPadding` = `root.resolvedStyle.height - footer.worldBound.yMin` (if the footer has positive height, root height is positive and `yMin < rootHeight`);
   - it invokes `OnLayoutRecalculated(topPadding, bottomPadding)` if both are non-negative.
   The values are UI pixels; with ConstantPixelSize at scale 1 these are used as screen pixels (not tested on high-DPI devices).

`GameRenderer.UpdateCamera(headerHeight, footerHeight)` (`GameRenderer.cs`):
1. Takes `GridMarginInPixels` (`Vector4`, default 20 on each side, serialised on `GameUIManager`) from the UI manager.
2. Computes the board's world size: `(n - 1) * (cellSize + cellSpacing) + cellSize` for width and for height.
3. Computes the free screen area: `Screen.width - left - right` by `Screen.height - header - footer - top - bottom`.
4. Compares aspect ratios and sets the orthographic size so the board fits the free area along the constrained axis.
5. Positions the camera at the board centre (z = -10), shifted vertically by half the difference between the space below (footer plus margin) and above (header plus margin), converted to world units.

The camera is only fitted when a layout event fires; `RestartGame` does not call it, so a restart relies on the earlier fit (valid because the grid size does not change on restart). `gridWidth`/`gridHeight` are set in `InitializeGame` (called from `Start`) before the first layout callback, because the observer waits at least 100 ms.

## Interactions with other systems
- Colour profile supplies the in-game UXML ([configuration.md](configuration.md)).
- `GameRenderer` drives text and panel updates every frame and consumes the UI events ([presentation.md](presentation.md)).
- Input is not blocked by panels ([input.md](input.md)).
- The resume button depends on the save file ([save-system.md](save-system.md)).
- The three main-menu buttons and the menu flow: [configuration.md](configuration.md).

## Known issues
- The level-complete panel (and the next-level button) can never appear. [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)
- Aesthetic rework of gameplay and UI is planned. [CC-018](https://github.com/rtumelty/Cathartic-Colours/issues/30)
- Scene-name capitalisation (`"Gameplay Scene"` vs `Gameplay scene.unity`) and missing guards. [CC-025](https://github.com/rtumelty/Cathartic-Colours/issues/37)
- `status-label` does not exist in `GameUI.uxml`, so the status text is never displayed although `GameUIManager.UpdateGameState` writes it every frame (`Assets/Scripts/Gameplay/GameUIManager.cs`, `CacheUIElements`; `Assets/UI/GameUI.uxml`). [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40)
- `restart-button` is not unique in `GameUI.uxml`; `Q<Button>("restart-button")` returns the game-over panel's button, and `HookUpEvents` then subscribes the same element a second time through `gameOverPanel.Q`, so one click raises `OnRestartRequested` twice (two `RestartGame` runs). The level-complete panel's restart button is never hooked (`Assets/Scripts/Gameplay/GameUIManager.cs`, `HookUpEvents`). Derived from code reading; not run. [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40)
- `UnhookEvents` unsubscribes with new lambda instances (`clicked -= () => ...`), which never match the subscribed ones, so those handlers are not removed (`Assets/Scripts/Gameplay/GameUIManager.cs`, `UnhookEvents`). Harmless while the whole UI is destroyed with the scene. [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40)

## Related test cases
- [Main menu](../test-plan.md#main-menu-menu): TP-MENU-01 to TP-MENU-04, TP-MENU-06. [HUD](../test-plan.md#gameplay-hud-hud): TP-HUD-01 to TP-HUD-05 (TP-HUD-03 status label, CC-028).
- [Game over](../test-plan.md#game-over-over): TP-OVER-02, TP-OVER-04 (game-over panel, restart).
