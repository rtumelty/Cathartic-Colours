# Input

## Purpose
Describes how player input (keyboard, and touch swipes on devices) becomes a move command in the ECS world: which Unity Input System objects are used, how raw input is turned into one of four grid directions, and the lifecycle of the `MoveDirectionComponent` entity that carries the command. What a move does once accepted is in [ecs-frame-order.md](ecs-frame-order.md) and [merge-rules.md](merge-rules.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Gameplay/GameInputHandler.cs` | Keyboard action, direction mapping, `SendMoveCommand`, `OnSwipe` |
| `Assets/Scripts/Gameplay/SwipeDetector.cs` | Touch swipe detection, calls `GameInputHandler.OnSwipe` |
| `Assets/Scripts/ECS/Components/MoveDirectionComponent.cs` | The command component (`int2 Direction`) |
| `Assets/Scripts/ECS/Systems/InputProcessingSystem.cs` | Accepts or rejects the command |
| `Assets/Scripts/ECS/Systems/ClearMoveDirectionSystem.cs` | Destroys the command |
| `Assets/Prefabs/GameManager.prefab` | Carries `GameInputHandler` and `SwipeDetector` |
| `Assets/Input/GameControls.inputactions` | Project-wide Input Actions asset (not used by the game code) |

## How it works

### Input System setup
- Project setting `activeInputHandler: 1` (the new Input System only) (`ProjectSettings/ProjectSettings.asset`).
- `Assets/Input/GameControls.inputactions` has no generated wrapper class (`generateWrapperCode: 0` in its `.meta`). It is registered as the **project-wide actions** asset (`ProjectSettings/ProjectSettings.asset` preloaded assets and `ProjectSettings/EditorBuildSettings.asset` key `com.unity.input.settings.actions`). Contents:

| Action map | Actions | Bindings |
|---|---|---|
| `UI` | `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `RightClick`, `MiddleClick`, `ScrollWheel`, `TrackedDevicePosition`, `TrackedDeviceOrientation` | The standard Unity UI action set (gamepad, joystick, keyboard WASD/arrows for Navigate, mouse, pen, touch, XR) |
| `Gameplay` | `Move` (Value, `Vector2`) | Gamepad left stick and D-pad; a `2DVector` composite on the arrow keys; a second `2DVector` composite on W/A/S/D |

  Control schemes: Keyboard&Mouse, Gamepad, Touch, Joystick. No script, scene or prefab references this asset by name or guid (search of `Assets/Scripts`, `Assets/Scenes`, `Assets/Prefabs`).
- **The game does not use that asset.** `GameInputHandler` builds its own actions in code (`Assets/Scripts/Gameplay/GameInputHandler.cs`):
  - `Awake`: `GetComponent<PlayerInput>()`, adding a `PlayerInput` component at runtime if there is none. The prefab has none (`Assets/Prefabs/GameManager.prefab` carries `GameInputHandler`, `GameRenderer` and `SwipeDetector` on the root object), so a `PlayerInput` is always added at runtime.
  - `OnEnable`: if `playerInput.actions` is null, creates an empty `InputActionAsset` with `ScriptableObject.CreateInstance`; finds or adds the action map `Gameplay` and the action `Move` (Value type); if `Move` is not found, adds it with a default binding of `<Keyboard>/upArrow` plus a `2DVector` composite: Up = up arrow and W, Down = down arrow and S, Left = left arrow and A, Right = right arrow and D. It enables the action and subscribes `OnMove` to `performed`. `OnDisable` unsubscribes and disables it.
  - Because the new asset is empty, the `Move` action is always created by this code and the gamepad bindings in `GameControls.inputactions` are not available in the game. Whether `PlayerInput` behaves as expected when its `actions` are assigned after it is already enabled was not run; unverified.
  - `PlayerInput` is not used for messages or events; only the action's own `performed` callback is used.

### Keyboard path (`GameInputHandler.OnMove`)
1. Reads the composite's `Vector2` (`context.ReadValue<Vector2>()`).
2. Maps it to an `int2` direction:
   - if `|x| > |y|`: `direction.x = +1` for positive x, `-1` for negative;
   - else if `|y| > 0.1`: `direction.y = +1` for positive y, `-1` for negative;
   - otherwise (including `0,0`, as when a key is released) no direction is produced and nothing is sent.
   A diagonal press (two keys held; the composite yields about 0.707 on both axes) fails the strict `|x| > |y|` test, so it resolves to a vertical move.
3. Calls `SendMoveCommand(direction)`.

Directions are grid directions: `(1, 0)` right, `(-1, 0)` left, `(0, 1)` up, `(0, -1)` down. The renderer places y increasing upwards (`GameRenderer.UpdateVisuals`), so "up" on the keyboard and "up" on screen agree.

### Swipe path (`SwipeDetector` and `GameInputHandler.OnSwipe`)
`Assets/Scripts/Gameplay/SwipeDetector.cs`:
- Declared as `Gameplay.CatharticColours.SwipeDetector`: the class sits in a namespace `CatharticColours` **nested inside** `namespace Gameplay`. The prefab's serialised type is `Assembly-CSharp::Gameplay.CatharticColours.SwipeDetector` (`Assets/Prefabs/GameManager.prefab`), so this is how it is actually referenced.
- Fields: `swipeThreshold` (default 50, prefab value 50, in pixels) and `inputHandler` (`GameInputHandler`, assigned to the same object's handler in the prefab).
- `Update` runs only when `Touchscreen.current != null`. On `primaryTouch.press.wasPressedThisFrame` it stores the start position and sets `isSwiping`. On `wasReleasedThisFrame` while swiping it computes `end - start`; if its magnitude is at least `swipeThreshold` it calls `inputHandler.OnSwipe(swipe.normalized)`. `isSwiping` is cleared on release. There is no time limit, no mid-swipe trigger, and **no mouse support**: dragging with a mouse on a desktop does nothing.
- `GameInputHandler.OnSwipe(Vector2)` picks the larger axis (`|x| > |y|` gives horizontal, otherwise vertical, with sign `> 0` for positive) and calls `SendMoveCommand`. There is no 0.1 dead-zone as in the keyboard path. A diagonal tie resolves to vertical.
- Touch input is read directly from the device, not through the UI: swipes that start on a UI button or over the game-over panel still count.

### `SendMoveCommand` and the `MoveDirectionComponent` lifecycle
`GameInputHandler.SendMoveCommand(int2)`:
1. Returns if `World.DefaultGameObjectInjectionWorld` is null.
2. Creates an `EntityQuery` for `MoveDirectionComponent`; if any such entity exists it returns (a command already pending is never replaced or queued).
3. Otherwise creates a new entity with `MoveDirectionComponent { Direction = direction }`.

Lifecycle:

| Stage | Where | What happens |
|---|---|---|
| Create | `SendMoveCommand`, from `Update`-time input callbacks (outside ECS) | Entity created if none exists |
| Accept / reject | `InputProcessingSystem` | Returns without effect if `!WaitingForInput`, `GameOver`, there is no command or the direction is `(0,0)`. Otherwise arms the blocks, starts the jelly wobble, sets `WaitingForInput = false` and increments `MoveCount` |
| Use | `BlockMovementSystem` | Reads the direction (it also emits `Block_Move` whenever a command exists, even if rejected) |
| Destroy | `ClearMoveDirectionSystem` (after `SpawnColorSystem`) | Destroys the command entity every frame it exists, whether accepted or not |

The full per-frame order is in [ecs-frame-order.md](ecs-frame-order.md#one-player-move-frame-by-frame). Consequences: at most one move per frame; input received while the game is not waiting (during the move frame) or after game over is discarded at the end of that frame, not buffered; a held key does not repeat (the `Move` action only performs on value changes).

### Input blocking while UI is open
There is none in the input code. `GameInputHandler` and `SwipeDetector` do not check for open panels, `UIDocument` focus, or pointer-over-UI. The only blocking is the simulation's: `InputProcessingSystem` ignores commands once `GameOver` is true, and the game-over panel (`GameUIManager.UpdateGameState`) is a full-screen overlay for pointer events on UI but does not stop keyboard or touch swipes reaching the handler. The level-complete panel can never show ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)). See [ui-layout.md](ui-layout.md).

## Interactions with other systems
- Command entity consumed by [ecs-frame-order.md](ecs-frame-order.md) systems; move rules in [merge-rules.md](merge-rules.md).
- `InputProcessingSystem` calls `GameManager.JellyManager.StartWobble` with the move direction ([presentation.md](presentation.md), [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)).
- Panels that overlay the board: [ui-layout.md](ui-layout.md).
- The handler lives on the `GameManager` prefab with `GameRenderer` ([configuration.md](configuration.md)).

## Known issues
- A rejected move (for example a swipe or key press after game over) still plays the move sound. [CC-023](https://github.com/rtumelty/Cathartic-Colours/issues/35)
- Presentation is triggered from simulation (`InputProcessingSystem` calls `GameManager.JellyManager` with no null check). [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)
- `GameControls.inputactions` (project-wide actions, including gamepad and a `Gameplay/Move` action) is unused: `GameInputHandler` creates its own empty `InputActionAsset` and adds a keyboard-only `Move` action in code, so there are two definitions of the same controls and gamepads do not work (`Assets/Scripts/Gameplay/GameInputHandler.cs`, `OnEnable`). [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42)
- `SwipeDetector` handles touchscreens only; a mouse drag does nothing, so swipe cannot be tested in the editor without a touch simulator (`Assets/Scripts/Gameplay/SwipeDetector.cs`, `Update`). [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42)
- `SwipeDetector` sits in the nested namespace `Gameplay.CatharticColours`, unlike every other gameplay class (`Assets/Scripts/Gameplay/SwipeDetector.cs`); renaming it would break the prefab's serialised type reference. [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42)
- keyboard and swipe paths use different dead-zone rules (`|y| > 0.1` for keyboard, none for swipe) and both resolve diagonal ties to vertical (`Assets/Scripts/Gameplay/GameInputHandler.cs`, `OnMove` and `OnSwipe`). [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42)

## Related test cases
- [Standard](../test-plan.md#standard-mode-std): TP-STD-02 (keyboard), TP-STD-08 (swipe), TP-STD-09 (gamepad, CC-030).
- [Game over](../test-plan.md#game-over-over): TP-OVER-03 (input ignored after game over).
