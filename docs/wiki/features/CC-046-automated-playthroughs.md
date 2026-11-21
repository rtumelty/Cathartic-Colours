# CC-046 — Automated playthroughs from menu to gameplay

| Field | Value |
|---|---|
| Issue | [#97](https://github.com/rtumelty/Cathartic-Colours/issues/97) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-046-automated-playthroughs` (from `develop`, created by the planning session on approval) |
| PR | — |
| Decision | [ADR-006](../decisions/ADR-006-official-test-tools.md), playthrough tests through Unity's official test packages |
| Follow-up | [CC-047](https://github.com/rtumelty/Cathartic-Colours/issues/102), the same playthroughs on an Android device (the primary use case) |
| Related | [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8) (PlayMode smoke tests), [test-plan](../test-plan.md), [input](../systems/input.md), [ui-layout](../systems/ui-layout.md), [save-system](../systems/save-system.md) |

## Goal
An agent can play the real game the way a tester does: navigate the main menu, start or resume a game, move with real input, and reach game over. It checks each test-plan case's expected result and reports pass or fail with evidence (board state, screenshots, audio events). Cases that can be automated become PlayMode tests that run from the command line. Other cases can be run by an agent writing a short test with the same driver.

## Today (verified on `develop`, 2026-11-21)
- **PlayMode tests** (`Assets/Tests/PlayMode/`, CC-002.4) are two smoke tests:
  - **Main menu:** presses `start-button` by finding it in the scene.
  - **Gameplay:** creates a `MoveDirectionComponent` entity directly, which skips the real input path (`GameInputHandler`, `SwipeDetector`).
  - **Shared support:** `PlayModeTestSupport` redirects saves, loads scenes and reads ECS singletons.
- **Input:** keyboard and gamepad come through `GameControls.inputactions` (`Gameplay/Move`), touch and mouse through `SwipeDetector`'s pointer reads. All of it is Input System.
- **UI:** UI Toolkit. Every control has a name (`start-button`, `resume-game-button`, `gamemode-dropdown`, `width-slider`, `spawn-toggle`, …), found with `root.Q<>()`.
- **Test plan:** 918 lines across 11 areas, written for a human. It says random spawns mean "a tester cannot script a board" and some cases may be "not reached".
- **Packages:** Test Framework 1.8.0 and Input System 1.20.1. No UI test package.

## Approach
- **D2 — Official tools first** (Ronan's rule from CC-043):
  - [UI Test Framework](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.ui.test-framework.html) (`com.unity.ui.test-framework`, a Unity 6.3+ core package) for UI Toolkit: its `PanelSimulator` clicks, types and navigates on a panel.
  - The Input System's `InputTestFixture` for virtual keyboards, gamepads and touchscreens. A move then goes through `GameInputHandler` and `SwipeDetector` exactly as a player's does.
  - Custom code only fills a gap, and the gap is recorded here and in ADR-006.
- **D3 — A test-only `GameDriver`** in a new test-support assembly (editor and test builds only, never in the player). It has a small, readable API that an agent can write steps with:
  - `StartFromMenu(mode, width, height, spawn)` and `Resume()`, through the real menu controls;
  - `Move(Up)`, `Swipe(Left)` and `Gamepad(Down)`, through real input devices;
  - `Board()`, which returns the board in **test-plan notation** (`Rs . Gm`), `Score`, `MoveCount`, `IsGameOver`, and the HUD labels' text;
  - `Screenshot("TP-STD-04-after")`, saved to the run's report folder;
  - `AudioEvents()`, the FMOD event paths raised since the last call.
- **D4 — Known boards through the real resume path.** `GameDriver.ResumeWith(board, mode, seed)` writes a save file in the redirected save folder (`SaveFilePathOverride`) and presses Resume. Cases can then start from an exact board and random seed, using code the game already has. The player's real save is never touched. The game itself gets no test hooks.
- **D5 — Runner and report for agents.** `tools/run-playthrough.ps1 [-Case TP-STD-04 | -Area STD]`:
  - runs the matching PlayMode tests in batchmode (`-testFilter` on test categories named after the case ids);
  - writes `TestResults/playthrough/<timestamp>/`, holding the results XML, the log, the screenshots and a `report.md` with one line per case (pass, fail or not automated, with its reason).

  The agent reads the report and screenshots, and can open the images itself.
- **D6 — Which cases are automated.** Every case in these areas, where it can be checked in the editor:
  - Main menu (MENU), Standard (STD), Color Merge (CM), Advanced Color Merge (ACM);
  - Gameplay HUD (HUD), Game over (OVER), Save/Resume (SAVE);
  - Audio (AUDIO), at the level of "this event was raised".

  These stay manual, each marked with its reason in the test plan:
  - **Android build (DROID):** install, device performance, touch feel;
  - how audio *sounds*;
  - visual polish beyond a screenshot check;
  - **Project tracking (TRACK).**

  The test plan gains an `Automated` field per case, naming its test.
- **Out of scope:** on-device playthroughs on the Android build. They are [CC-047](https://github.com/rtumelty/Cathartic-Colours/issues/102), planned in parallel; Ronan named the device as the primary use case (2026-11-21).
- **D7 — Keep the driver ready for a device** (Ronan, 2026-11-21):
  - `GameDriver` and the case tests use only runtime APIs, with no `UnityEditor` calls, so CC-047 can run the same tests in an Android player.
  - Screenshots and the audio log go to a folder given to the driver, not a fixed editor path.
  - Anything that can only work in the editor is isolated and named on this page.
- **Assembly:** a new `CatharticColours.TestSupport` assembly (test-only, `UNITY_INCLUDE_TESTS`) referenced by the PlayMode tests. `PlayModeTestSupport` moves into it.

## Deliverables

### CC-046.1 ([#98](https://github.com/rtumelty/Cathartic-Colours/issues/98)) — Prove the official tools on our scenes, and ADR-006
**Risk:** High · **Worker:** primary (sets the testing pattern and the driver's shape) · **Review:** primary

A spike, kept as tests. It answers four questions in batchmode, from the command line:
1. Can `PanelSimulator` drive the scene's existing `UIDocument` panels, rather than a panel the fixture creates? Click `start-button`, pick a mode in `gamemode-dropdown`, move a slider.
2. Does an `InputTestFixture` keyboard and touchscreen produce moves through `GameInputHandler` and `SwipeDetector`?
3. Does a screenshot in a batchmode PlayMode run contain the rendered frame, or is it black?
4. Can audio events be read without FMOD playing sound? Count `AudioEventComponent` entities before `AudioSystem` consumes them, or listen in the test.

Then:
- Write ADR-006 with the answers, and any gap with what fills it.
- Create the test-support assembly with a first `GameDriver` (`StartFromMenu`, `Move`, `Board`).
- Move the two smoke tests onto it, so the gameplay smoke test moves through real input.

**Definition of Done** (template plus):
- [x] The four answers recorded on this page and in ADR-006; any fallback named (for example raw `ClickEvent`s through the panel, if the simulator can't attach)
- [x] Smoke tests pass through the driver; full EditMode and PlayMode pass from the command line

**Stop rule:** if the official tools can't do question 1 or 2 at all, stop and tell Ronan before building custom input or UI code.

### CC-046.2 ([#99](https://github.com/rtumelty/Cathartic-Colours/issues/99)) — The full driver: known boards, readouts, screenshots, audio
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. `ResumeWith(board, mode, seed)`, through a written save file (D4); `Swipe` and `Gamepad`; HUD text; `Screenshot`; `AudioEvents`; `WaitUntil(condition, timeout)`.
2. A notation parser and printer shared with the EditMode `TestBoards` helper, so both use the same notation.
3. A [test-automation](../systems/test-automation.md) systems page: the driver API, how to write a case, and what can't be automated.

**Tests:**
- driver self-tests: a board written then resumed reads back identically;
- one move of each input kind;
- a screenshot file exists and isn't blank;
- a merge raises its merge audio event.

**Definition of Done:**
- [x] Self-tests pass; full EditMode and PlayMode pass
- [x] test-automation page written

### CC-046.3 ([#100](https://github.com/rtumelty/Cathartic-Colours/issues/100)) — Automate the test-plan cases
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. One PlayMode test per case in MENU, STD, CM, ACM, HUD, OVER, SAVE and AUDIO (D6). Each has the category of its case id (`[Category("TP-STD-04")]`) and the case's own steps and expected result, starting from a known board instead of "play until it happens".
2. Add the `Automated` field to the test plan: the test name, or "manual" with a reason.
3. A case whose expected result doesn't match the game is **not** fixed here. It becomes a bug issue and the test is marked to skip with the issue number, so the run stays green and the bug is visible.

**Tests:** the cases themselves. The whole PlayMode suite must stay under about 10 minutes.

**Definition of Done:**
- [ ] Every case in the D6 areas automated, or marked manual with its reason
- [ ] Bugs found filed as issues (`gh`), linked from the skipped tests

### CC-046.4 ([#101](https://github.com/rtumelty/Cathartic-Colours/issues/101)) — Runner script, agent guide, wrap-up and PR
**Risk:** Low · **Worker:** Haiku · **Review:** primary
1. `tools/run-playthrough.ps1` with `-Case`, `-Area` and `-All`, writing the report folder (D5).
2. Agent guide on the test-automation page:
   - how to run a case;
   - how to read the report;
   - how to run a manual case that has no test by writing a scratch test with the driver.
3. **Schema change, approved with this plan:** project `CLAUDE.md` §4 gains the playthrough command next to the test commands. Logged as `schema`.
4. Feature test plan; index and log.
5. Device build at the PR, as user-level §2.3 requires. This feature adds no player code, so the build is a check that test-only code stays out of the player.
6. PR and quiz.

## Workflow exception (Ronan, 2026-11-21)
- No approval stops between deliverables, and no manual checks. One PR at the end.
- Sub-issues go to In progress, then In review on push. The planning session closes them after the merge.
- Long builds don't hold up commits.
- **Stop and tell Ronan over Remote Control** if the CC-046.1 stop rule triggers, if the driver needs a hook in game code, or if scope changes.
- Runs in the sandbox (`E:\Projects\Cathartic-Colours`).

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-046.1 | High | primary | Spike plus pattern-setting driver and ADR |
| CC-046.2 | Medium | Sonnet | Against the driver shape from .1 |
| CC-046.3 | Medium | Sonnet | Many small tests, spec is the test plan |
| CC-046.4 | Low | Haiku | Script, docs, build and PR |

## MANUAL (Ronan)
None during the work. During PR review, run the playthrough script for one area and read its report:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\run-playthrough.ps1 -Area STD
```

## Decisions
- **D1 — Workflow without stops** (Ronan, 2026-11-21), as CC-008.
- **D2 to D6** as in Approach (proposed by planning; approved with the plan, 2026-11-21).
- **D7 — Driver ready for a device** (Ronan, 2026-11-21), as in Approach.

## CC-046.1 answers (spike, 2026-11-21)
Recorded in full in [ADR-006](../decisions/ADR-006-official-test-tools.md). The spike tests are `Assets/Tests/PlayMode/ToolSpikeTests.cs`.
1. **UI:** yes. `RuntimeUITestFixture.SetUIContent` attaches the UI Test Framework's simulator to each scene's own `UIDocument`. It clicks the dropdown entries, drags the sliders and presses Start. This needs the fixture as the test's base class (`PlaythroughTest`). In the editor it also needs the panel laid out at phone size.
2. **Input:** yes. `InputTestFixture` keyboard presses go through `GameInputHandler`, and a queued touchscreen swipe goes through `SwipeDetector`.
3. **Screenshots:** not from the screen in batchmode (no end of frame). They work by rendering the camera and the UI into textures and compositing them.
4. **Audio:** yes. `AudioEventRecorderSystem` runs between `BoardStepSystem` and `AudioSystem`; FMOD still plays.

**Editor-only parts (D7):**
- the panel retarget to a 1080x1920 render texture;
- the screenshot composite and its camera framing.

Both are behind `Application.isEditor`. A device uses its real screen and the end-of-frame screen read. Device test builds must be development builds, because `InputTestFixture` only saves and restores input state there.

## Divergences
- **Case categories use underscores** (CC-046.1, implementation detail): NUnit rejects `-` in category names, so TP-STD-04's test has `[Category("TP_STD_04")]`. The runner script (CC-046.4) maps ids to categories.
- **Tests derive from `PlaythroughTest`** (CC-046.1, implementation detail). The UI Test Framework's event helpers are internal and only set up by its fixture, so the driver takes the fixture rather than creating its own simulator.
- **`Screenshot` and `AudioEvents` arrived in CC-046.1**, because the spike needed them for questions 3 and 4. CC-046.2 still adds known boards, `Swipe`, `Gamepad` and the self-tests.
- **Core's `AssemblyInfo.cs` gains `InternalsVisibleTo("CatharticColours.TestSupport")`**, so the moved `PlayModeTestSupport` can still set `SaveGameManager.SaveFilePathOverride`. This grants access only and adds no game hook; the PlayMode test assembly already had the same grant.
- **Gap fillers in the test-support assembly:** `PanelClock`, `PngWriter` and `AudioEventRecorderSystem`. Each is named in ADR-006 with its reason.

## Feature test plan
1. `tools\run-playthrough.ps1 -Case TP-STD-04` passes, and the report has before and after screenshots.
2. `tools\run-playthrough.ps1 -All` runs every automated case; the report lists every case in the D6 areas.
3. Your real save is untouched after a run.

## Status log
- 2026-11-21: Planned and approved by Ronan; sub-issues #98-#101; device follow-up CC-047 (#102) created
