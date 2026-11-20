# Input

## Purpose
Describes how player input (keyboard, gamepad, and touch or mouse swipes) becomes a move command in the ECS world: which Unity Input System objects are used, how raw input is turned into one of four grid directions, and the lifecycle of the `MoveDirectionComponent` entity that carries the command. What a move does once accepted is in [ecs-frame-order.md](ecs-frame-order.md) and [merge-rules.md](merge-rules.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Gameplay/GameInputHandler.cs` | Reads `Gameplay/Move` from the assigned actions asset, one-tilt-one-move arming, `SendMoveCommand`, `OnSwipe` |
| `Assets/Scripts/ECS/Utilities/InputDirection.cs` | Pure direction rule (dead zone, dominant axis) |
| `Assets/Scripts/Gameplay/SwipeDetector.cs` | Touch or mouse-drag swipe detection, calls `GameInputHandler.OnSwipe` |
| `Assets/Scripts/ECS/Components/MoveDirectionComponent.cs` | The command component (`int2 Direction`) |
| `Assets/Scripts/ECS/Systems/BoardStepSystem.cs` | Accepts or rejects the command and runs the move |
| `Assets/Scripts/ECS/Systems/ClearMoveDirectionSystem.cs` | Destroys the command |
| `Assets/Prefabs/GameManager.prefab` | Carries `GameInputHandler` (with the `actions` reference) and `SwipeDetector` |
| `Assets/Input/GameControls.inputactions` | Project-wide Input Actions asset; `Gameplay/Move` drives keyboard and gamepad input |

## How it works

### Input System setup
- Project setting `activeInputHandler: 1` (the new Input System only) (`ProjectSettings/ProjectSettings.asset`).
- `Assets/Input/GameControls.inputactions` has no generated wrapper class (`generateWrapperCode: 0` in its `.meta`). It is registered as the **project-wide actions** asset (`ProjectSettings/ProjectSettings.asset` preloaded assets and `ProjectSettings/EditorBuildSettings.asset` key `com.unity.input.settings.actions`). Contents:

| Action map | Actions | Bindings |
|---|---|---|
| `UI` | `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `RightClick`, `MiddleClick`, `ScrollWheel`, `TrackedDevicePosition`, `TrackedDeviceOrientation` | The standard Unity UI action set (gamepad, joystick, keyboard WASD/arrows for Navigate, mouse, pen, touch, XR) |
| `Gameplay` | `Move` (Value, `Vector2`) | Gamepad left stick and D-pad; a `2DVector` composite on the arrow keys; a second `2DVector` composite on W/A/S/D |

  Control schemes: Keyboard&Mouse, Gamepad, Touch, Joystick. Referenced by `GameManager.prefab` (`GameInputHandler.actions`).
- **The game uses that asset** (CC-030). `GameInputHandler` has `[SerializeField] private InputActionAsset actions`, assigned to `GameControls.inputactions` on `Assets/Prefabs/GameManager.prefab` (main asset object `fileID: -944628639613478452`, the same reference `ProjectSettings` uses for the project-wide actions). `OnEnable` does `actions.FindAction("Gameplay/Move", throwIfNotFound: false)`; if the asset or the action is missing it logs one `Debug.LogError` and input handling stays off (no exception). It subscribes `performed` and `canceled`, then enables the action; `OnDisable` unsubscribes and disables it. No `PlayerInput` component is created or needed (nothing else used it). Because the bindings come from the asset, keyboard (arrows, WASD), gamepad (left stick, D-pad) all work and any binding change is made in the asset.

### The direction rule (`ECS.Utilities.InputDirection.Resolve`)
One pure function (`Assets/Scripts/ECS/Utilities/InputDirection.cs`, no Input System types, tested in `Assets/Tests/EditMode/Core/InputDirectionTests.cs`) used by keyboard, gamepad and swipe:
- If the vector's magnitude is below the dead zone (`DeadZone = 0.5`) there is no direction, `(0, 0)`.
- Otherwise the dominant axis wins: `|x| > |y|` gives left/right, else up/down. An exact tie (a perfect diagonal) resolves to vertical.

Directions are grid directions: `(1, 0)` right, `(-1, 0)` left, `(0, 1)` up, `(0, -1)` down. The renderer places y increasing upwards (`GameRenderer.UpdateVisuals`), so "up" on the keyboard and "up" on screen agree.

### Keyboard, D-pad and stick path (`GameInputHandler.OnMove`)
One tilt (or key press) sends one move:
1. Reads the `Vector2` from `Move` (key composites give magnitude 1, or about 0.707 per axis with two keys; a stick gives analogue values).
2. While **armed**, it calls `InputDirection.Resolve`; a direction other than `(0, 0)` disarms the handler and calls `SendMoveCommand`.
3. While **disarmed**, further values send nothing. It re-arms when the magnitude drops below `ReleaseZone = 0.25` (stick back near centre) or when the action is `canceled` (all keys released).
Consequences: a held key or stick does not repeat; a wobbling stick that stays past 0.25 does not spam moves; pressing a second key while the first is still held does not change direction or fire (release everything, then press again). A move that `SendMoveCommand` drops because one is already pending is not retried.

### Swipe path (`SwipeDetector` and `GameInputHandler.OnSwipe`)
`Assets/Scripts/Gameplay/SwipeDetector.cs`:
- Declared as `Gameplay.CatharticColours.SwipeDetector`: the class sits in a namespace `CatharticColours` **nested inside** `namespace Gameplay`. The prefab's serialised type is `Assembly-CSharp::Gameplay.CatharticColours.SwipeDetector` (`Assets/Prefabs/GameManager.prefab`), so this is how it is actually referenced and the name must not change.
- Fields: `swipeThreshold` (default 50, prefab value 50, in pixels) and `inputHandler` (`GameInputHandler`, assigned to the same object's handler in the prefab; `Update` does nothing if it is null).
- Pointer source: the touchscreen's `primaryTouch` while a touch is pressed or just pressed/released; otherwise the mouse (`leftButton` and `position`). So a left-button mouse drag swipes in the editor and on desktop.
- On press it stores the start position and sets `isSwiping`. On release while swiping it computes `end - start`; if its magnitude is at least `swipeThreshold` it calls `inputHandler.OnSwipe(swipe.normalized)`. There is no time limit and no mid-swipe trigger. A click without movement is below the threshold, so clicking UI buttons with the mouse does not move the board.
- `GameInputHandler.OnSwipe(Vector2)` passes the normalised vector through `InputDirection.Resolve` (always above the dead zone) and calls `SendMoveCommand`.
- Pointer input is read directly from the device, not through the UI: a swipe that starts on a UI button or over the game-over panel still counts if it travels far enough.

### `SendMoveCommand` and the `MoveDirectionComponent` lifecycle
`GameInputHandler.SendMoveCommand(int2)`:
1. Returns if `World.DefaultGameObjectInjectionWorld` is null.
2. Creates an `EntityQuery` for `MoveDirectionComponent`, checks whether it is empty and disposes the query; if any such entity exists it returns (a command already pending is never replaced or queued).
3. Otherwise creates a new entity with `MoveDirectionComponent { Direction = direction }`.

Lifecycle:

| Stage | Where | What happens |
|---|---|---|
| Create | `SendMoveCommand`, from `Update`-time input callbacks (outside ECS) | Entity created if none exists |
| Accept / reject and use | `BoardStepSystem` | Rejects without effect or sound if `!WaitingForInput` or `GameOver` (and `BoardSimulation.Step` rejects any direction other than the four unit vectors). Otherwise runs the move, sets `WaitingForInput = false`, increments `MoveCount` and records `LastMoveDirection` |
| Destroy | `ClearMoveDirectionSystem` (after `BoardStepSystem`) | Destroys the command entity every frame it exists, whether accepted or not |

The full per-frame order is in [ecs-frame-order.md](ecs-frame-order.md#one-player-move-frame-by-frame). Consequences: at most one move per frame; input received while the game is not waiting (during the move frame) or after game over is discarded at the end of that frame, not buffered; a held key or tilted stick does not repeat (see the arming rule above).

### Input blocking while UI is open
There is none in the input code. `GameInputHandler` and `SwipeDetector` do not check for open panels, `UIDocument` focus, or pointer-over-UI. The only blocking is the simulation's: `BoardStepSystem` ignores commands once `GameOver` is true, and the game-over panel (`GameUIManager.UpdateGameState`) is a full-screen overlay for pointer events on UI but does not stop keyboard or touch swipes reaching the handler. The level-complete panel can never show ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31)). See [ui-layout.md](ui-layout.md).

## Interactions with other systems
- Command entity consumed by [ecs-frame-order.md](ecs-frame-order.md) systems; move rules in [merge-rules.md](merge-rules.md).
- The jelly wobble starts when `GameRenderer` sees `MoveCount` increase, in `LastMoveDirection` ([presentation.md](presentation.md)); input code does not call presentation.
- Panels that overlay the board: [ui-layout.md](ui-layout.md).
- The handler lives on the `GameManager` prefab with `GameRenderer` ([configuration.md](configuration.md)).

## Known issues
- `SwipeDetector` sits in the nested namespace `Gameplay.CatharticColours`, unlike every other gameplay class (`Assets/Scripts/Gameplay/SwipeDetector.cs`); renaming it would break the prefab's serialised type reference.
- Input is not blocked while UI panels are open (see above).

## Related test cases
- [Standard](../test-plan.md#standard-mode-std): TP-STD-02 (keyboard), TP-STD-08 (swipe), TP-STD-09 (gamepad, CC-030).
- [Game over](../test-plan.md#game-over-over): TP-OVER-03 (input ignored after game over).
