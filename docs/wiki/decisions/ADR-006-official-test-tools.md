# ADR-006 — Playthrough tests through Unity's official test packages

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-21 |
| Roadmap id | [CC-046](https://github.com/rtumelty/Cathartic-Colours/issues/97) |
| Supersedes | — |

## Context
Agents should run the manual [test plan](../test-plan.md) by playing the real game: menu, real input, gameplay, game over. Until now the two PlayMode smoke tests (CC-002.4) found the Start button with `root.Q<>()` and sent it a `NavigationSubmitEvent`, and they made a move by creating a `MoveDirectionComponent` entity directly. That skipped the real input path (`GameInputHandler`, `SwipeDetector`).

Constraints:
- **Official tools first** (Ronan's rule from CC-043). Custom code may only fill a gap, and the gap must be named.
- **No test hooks in the game.**
- **Device-ready** (D7): the driver and the tests use runtime APIs only, so CC-047 can run them in an Android player.
- **Batchmode:** they run from the command line in a batchmode editor.

The spike in CC-046.1 (`Assets/Tests/PlayMode/ToolSpikeTests.cs`, kept as tests) answered four questions.

| # | Question | Answer |
|---|---|---|
| 1 | Can the UI Test Framework drive the scenes' own `UIDocument` panels? | **Yes.** `RuntimeUITestFixture.SetUIContent(doc)` attaches its `RuntimePanelSimulator` to a scene's document. Clicks open `gamemode-dropdown` and pick an entry, drag the slider handles and press `start-button`, and the menu's own callbacks run. Two conditions: the fixture must be the test's base class, and in the editor the panel must be laid out at a phone size (see the gaps below). |
| 2 | Do Input System test devices make moves through `GameInputHandler` and `SwipeDetector`? | **Yes.** With an `InputTestFixture` keyboard, an arrow key press makes a move through the `Gameplay/Move` action. A touchscreen swipe (begin, two frames, end 200 px away) makes a move through `SwipeDetector`. Touch events must be queued (`queueEventOnly: true`), so the player loop's input update processes them as on a device. |
| 3 | Does a batchmode screenshot hold the rendered frame? | **Not from the screen.** A batchmode editor never reaches the end of a frame (`WaitForEndOfFrame` throws), and the back buffer can't be read. **Rendering into textures works:** the main camera with `Camera.Render()` into a render texture, and the UI drawn into the panel's target texture, composited. |
| 4 | Can audio events be read without FMOD playing sound? | **Yes.** A test-only ECS system (`AudioEventRecorderSystem`) runs after `BoardStepSystem` and before `AudioSystem` and copies each event path. The driver adds it to the default world. FMOD still plays each event as before. |

## Options

### Option A — Official packages, with small gap fillers
- **Description:**
  - UI: UI Test Framework (`com.unity.ui.test-framework` 6.6.0, a core package), through `RuntimeUITestFixture`.
  - Input: Input System `InputTestFixture` virtual devices.
  - Readouts: from the ECS world and the named HUD labels.
  - A `GameDriver` wraps these in a short API for tests and agents.
- **Pros:**
  - Every click and key goes through the same code as a player's.
  - Both packages document running in players, including Android.
  - No game code changes.
- **Cons:** a few gaps need test-side code (below).

### Option B — Custom event injection
- **Description:** send `ClickEvent`s and `NavigationSubmitEvent`s to elements found by name, and create move entities directly, as the smoke tests did.
- **Pros:** no new packages.
- **Cons:**
  - Skips the real input path.
  - A synthetic `ClickEvent` isn't handled by `Button`'s `Clickable`.
  - Breaks Ronan's official-tools rule.

### Option C — Third-party automation (AltTester, Airtest, Appium)
- **Description:** an external agent drives a running build over a socket.
- **Pros:** works on devices without a test build.
- **Cons:**
  - Needs an SDK inside the game, which is a test hook.
  - A licence and a server.
  - Not official.

## Decision
**Option A.** The spike showed the official packages can drive the real menus and the real input path in batchmode. What they can't do is small, test-only and named below.

### Gaps and what fills them
| Gap | Filled by | Where it runs |
|---|---|---|
| The simulator's helpers log "EventHelpers.TestSetUp() was not called" unless the test class derives from the framework's fixture (`EventHelpers` is internal). | `PlaythroughTest`, a `RuntimeUITestFixture` subclass. It never creates its own `UIDocument` (`RecreatePanel` is empty) and never clears the scenes' panels. | Editor and player |
| The simulator replaces its panel's clock with a static that only moves when a test calls `FrameUpdate`. That would freeze our panels' scheduled work and USS transitions. | `PanelClock`: a hidden object that advances the simulated clock to real time every frame. | Editor and player |
| A batchmode editor's screen is 640x480. The menu's controls then overlap, and clicks land on the wrong element. Picking can't reach a document root made larger than the screen. | In the editor only, the driver points the scene's `PanelSettings.targetTexture` at a 1080x1920 render texture for the test, and restores the asset's values afterwards. | **Editor only** |
| No end-of-frame screen capture in batchmode. `ScreenCapture` and `EncodeToPNG` need the Screen Capture and Image Conversion engine modules, which the project doesn't include. | `Screenshot()`. In the editor it renders the camera into a 1080x1920 texture, frames the board as `GameRenderer` does but for that size, and composites the UI texture over it. In a player it reads the screen at the end of the frame. `PngWriter` encodes the PNG with `System.IO.Compression`. | Composite: **editor only**. Screen read: player |
| Audio events are created and destroyed in the same frame. | `AudioEventRecorderSystem`, `[DisableAutoCreation]`, added to the world by the driver. | Editor and player |
| NUnit categories can't contain `-`. | Case categories use underscores: `TP_STD_04` for TP-STD-04. The runner script maps one to the other. | Both |

## Consequences
- **Test classes derive from `PlaythroughTest`** and use `Driver`. The smoke tests moved onto it; the gameplay smoke test now moves with a real key press.
- **New package:** `com.unity.ui.test-framework` 6.6.0 in `Packages/manifest.json`. It is a test package, and the player never references it.
- **New assembly:** `CatharticColours.TestSupport` (`Assets/Tests/Support/`, `UNITY_INCLUDE_TESTS`). It holds the driver, the fixture base class, the notation helpers and the gap fillers above. `PlayModeTestSupport` moved there. Core's `InternalsVisibleTo` lists it, so it can set `SaveGameManager.SaveFilePathOverride`. That grants access only and adds no game behaviour.
- **For CC-047 (device):**
  - The editor-only parts (panel retarget, screenshot composite) are behind `Application.isEditor`.
  - `InputTestFixture` saves and restores the input state only in development builds (`DEVELOPMENT_BUILD || UNITY_EDITOR`), so device test builds must be development builds.
  - Layout on a device uses the real screen.
- **Documented in:** [test-automation](../systems/test-automation.md) (the driver API, how to write a case, what can't be automated).

## Status
Accepted, 2026-11-21 (CC-046.1).
