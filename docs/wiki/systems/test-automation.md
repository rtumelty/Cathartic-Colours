# Test automation

## Purpose
Describes the `GameDriver`, which plays the real game from the main menu through gameplay for PlayMode tests and for agents: what it is, why it exists, how to write a test with it, every call it offers, the board notation, and what it cannot do. The choice of tools is in [ADR-006](../decisions/ADR-006-official-test-tools.md); the plan is the [CC-046 feature page](../features/CC-046-automated-playthroughs.md). The assemblies and how to run the tests are in [assemblies](assemblies.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Tests/Support/GameDriver.cs` | The driver: menus, known boards, moves, readouts, screenshots, audio events, waiting |
| `Assets/Tests/Support/PlaythroughTest.cs` | Base class for tests: creates a driver before each test and ends it after |
| `Assets/Tests/Support/PlayModeTestSupport.cs` | Temp save folder, scene unloading and ECS query helpers |
| `Assets/Tests/Support/BoardNotation.cs` | The board notation: parse, format and compare. Shared with the EditMode `TestBoards` helper |
| `Assets/Tests/Support/AudioEventRecorderSystem.cs` | Test-only ECS system that records audio event paths |
| `Assets/Tests/Support/PanelClock.cs`, `PngWriter.cs` | Gap fillers named in ADR-006 (UI clock, PNG file writer) |
| `Assets/Tests/Support/CatharticColours.TestSupport.asmdef` | The test-support assembly. Referenced by the PlayMode tests and the EditMode `CatharticColours.Simulation.Tests` |
| `Assets/Tests/PlayMode/DriverSelfTests.cs` | Tests of the driver itself, on boards with known results |
| `Assets/Tests/PlayMode/SmokeTests.cs`, `ToolSpikeTests.cs` | The first tests built on the driver |

## How it works

### What the driver is, and why
The driver does what a tester does, through the same code a player uses:
- **Menus:** the UI Test Framework's simulator sends real pointer events to the scenes' own controls (`start-button`, `gamemode-dropdown`, the sliders).
- **Moves:** Input System test devices (a virtual keyboard, gamepad and touchscreen) feed `GameInputHandler` and `SwipeDetector`.
- **Readouts:** the board, score and move count come from the ECS world. The text labels come from the HUD.

The game has no test hooks. A test sees only what a player could do, plus read-only access to the ECS world. This is why a passing playthrough says something about the real game. See [ADR-006](../decisions/ADR-006-official-test-tools.md) for the options that were rejected.

### Writing a test
Derive from `PlaythroughTest`, use `[UnityTest]` and drive the game through `Driver`:

```csharp
[Category("CC046")]
public class MyCase : PlaythroughTest
{
    [UnityTest, Category("TP_STD_04"), Category("STD")]
    public IEnumerator SmallPairMergesToMedium()
    {
        yield return Driver.ResumeWith(". . . .|. . . .|. . Rs Rs|. . . .", GameMode.Standard);
        yield return Driver.Move(Direction.Right);
        Driver.AssertBoard(". . . .|. . . .|. . . Rm|. . . .");
        Assert.AreEqual(10, Driver.Score);
    }
}
```

- **Setup and teardown are automatic.** Before each test the driver redirects saves to a temp folder (the real save is never read or written), resets `GameManager` to a first launch and creates the virtual devices. After each test it unloads the scenes and restores everything.
- **Categories:** one per test-plan case, with the id's dashes turned into underscores (`TP-STD-04` becomes `TP_STD_04`), because NUnit rejects `-` in category names. Add the area as a second category (`STD`, `HUD`, and so on).
- **A move that is rejected makes no change.** Check `Driver.MoveCount` to tell an accepted move from a rejected one.
- **Any error logged during a test fails it** (Unity's default).
- Start from a known board with `ResumeWith`, not from a random game. Turn spawning off (the default) when the test needs an exact board.

### The driver API

**Setup** (the base class calls these)
| Call | What it does |
|---|---|
| `GameDriver.Begin(ui, reportFolder)` | Redirects saves, resets `GameManager`, adds the input devices, starts recording audio |
| `End()` | Unloads the scenes, restores input and the save path |
| `ReportFolder`, `SaveFolder` | Where screenshots go, and the temp save folder |
| `Keyboard`, `Gamepad`, `Touchscreen`, `Input` | The virtual devices and the `InputTestFixture`, for steps with no helper |

**Menus and UI**
| Call | What it does |
|---|---|
| `OpenMenu()` | Loads the main menu and attaches to its panel |
| `StartFromMenu(mode, width, height, spawnNextColour)` | Sets the controls and presses Start, then waits for gameplay |
| `Click(name)`, `Click(element)` | A simulated mouse click at the element's centre |
| `SelectDropdown(name, choice)` | Opens a dropdown and clicks the entry with that text |
| `SetSlider(name, value)` | Drags a `SliderInt` handle to the value |
| `SetToggle(name, value)` | Clicks a `Toggle` if it doesn't already have the value |
| `Find<T>(name)`, `Text(name)`, `IsShown(name)`, `Root`, `DescribeTree(...)` | Read the UI: find an element, read its text, check it is displayed, dump the element tree |
| `WaitForGameplay()` | Waits for the gameplay scene and its game state, then attaches to its panel |
| `SceneName`, `Simulate` | The active scene's name, and the UI simulator for steps with no helper |

**Known boards**
| Call | What it does |
|---|---|
| `ResumeWith(board, mode, seed, spawnNextColour, moveCount, score)` | Writes a save for the board, opens the menu, clicks Resume and waits for gameplay. This is the real resume path |
| `WriteSave(board, mode, ...)` | Only writes the save file, for cases that start from the menu themselves |

`ResumeWith` takes the grid size from the board text. `seed` sets the random generator state, so spawns repeat. A board with a `?` throws, because `?` is only for expected boards. A save with a move count of 0 or 1 counts as a first save, which removes older backup files. That is harmless in a temp folder.

**Moves** (each waits until the board has handled the move)
| Call | What it does |
|---|---|
| `Move(direction)` | Arrow key |
| `MoveWasd(direction)` | W, A, S or D |
| `Swipe(direction, distance)` | A touch from the screen centre. The game ignores swipes under 50 pixels, so `distance: 10` makes no move |
| `GamepadDpad(direction)` | A d-pad press |
| `GamepadStick(direction, holdSeconds)` | The left stick pushed fully over, held, then released. A held stick makes one move |

**Readouts**
| Call | What it does |
|---|---|
| `Board()` | The board in notation, top row first |
| `AssertBoard(expected, message)` | Fails with the first differing cell when the board differs. `?` allowed |
| `MoveCount`, `Score`, `Mode`, `GridWidth`, `GridHeight` | Game state from the ECS world |
| `IsGameOver`, `WaitingForInput` | Game state flags |
| `MovesText`, `ScoreText`, `StatusText` | The HUD labels (`move-count`, `score-label`, `status-label`), trimmed |
| `IsGameOverPanelShown` | Whether `game-over-panel` is displayed |

**Screenshots and audio**
| Call | What it does |
|---|---|
| `Screenshot(name, settleSeconds)` | Saves `<name>.png` in the report folder after waiting for block animations. The path is in `LastScreenshotPath` |
| `AudioEvents()` | The FMOD event paths raised since the last call, from `AudioEventRecorderSystem`, which runs before `AudioSystem`. FMOD still plays them |

**Waiting**
| Call | What it does |
|---|---|
| `WaitUntil(condition, what, timeoutFrames)` | Waits for a condition, or throws naming what it waited for |
| `Frames(count)` | Waits that many frames |

### Board notation
The same notation is used by the manual [test plan](../test-plan.md), the driver and the EditMode `TestBoards` helper (all through `BoardNotation`). Cells are separated by spaces and rows by `|`. **The first row is the top row**, so x grows to the right and y grows upwards.

| Token | Meaning |
|---|---|
| `.` | Empty cell |
| `i` | The white next-colour indicator |
| `Rs` | A colour letter (`R G B Y M C W`) and a size letter (`s m l`) |
| `?` | In an expected board only: a freshly resolved indicator, a Small Red, Green or Blue block |

Remember that a block moves one cell per move and merges only when the cell ahead is occupied when it is processed ([merge-rules](merge-rules.md)).

### What can't be automated
- Device performance and touch feel: frame rate, input latency, how a swipe feels under a thumb.
- How audio sounds. A test sees which events were raised, not the sound.
- Visual polish beyond screenshots. A screenshot is saved for a person or agent to look at; a test does not judge it.
- Project tracking cases (GitHub board, labels): they are about GitHub, not the game.
- FMOD Studio checks: the project's events and banks are edited and checked in FMOD Studio.
- Inspecting the editor's Hierarchy.

### Editor-only parts and device notes
Everything in `Assets/Tests/Support` and the PlayMode tests uses runtime APIs only, so the same tests can run in an Android player ([CC-047](https://github.com/rtumelty/Cathartic-Colours/issues/102)). The parts that behave differently in the editor are behind `Application.isEditor`:
- **Panel size:** a batchmode editor's screen is 640x480, where the menu's controls overlap. In the editor the driver points each panel at a 1080x1920 render texture for the test. On a device the real screen is used.
- **Screenshots:** a batchmode editor never reaches the end of a frame, so the driver renders the camera and the UI into textures and composites them. On a device it reads the screen at the end of the frame.
- **Camera framing for the screenshot** is also editor-only.

Device test builds must be development builds, because `InputTestFixture` saves and restores input state only there. Swipes start at the real screen's centre.

### Known behaviour worth knowing
- The tests redirect `SaveGameManager.SaveFilePathOverride`, which Core makes visible to the test-support assembly with `InternalsVisibleTo`. No game code was added for tests.
- Only one Unity process can open the project at a time, so run test batches one after another.

## Agent guide
Added in CC-046.4.

## Related
- [ADR-006](../decisions/ADR-006-official-test-tools.md): why the official packages, and the gaps the support code fills.
- [Assemblies](assemblies.md): the test assemblies and the command lines that run them.
- [Input](input.md): the input path the driver exercises.
- [Merge rules](merge-rules.md): what a move does, with worked examples.
- [Save system](save-system.md): the save schema `ResumeWith` writes.
