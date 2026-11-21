# CC-047 — Automated playthroughs on an Android device

| Field | Value |
|---|---|
| Issue | [#102](https://github.com/rtumelty/Cathartic-Colours/issues/102) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-047-device-playthroughs` (cut from `develop` on approval so the plan is on `origin`; the session merges `develop` into it after CC-046 merges, before any work) |
| PR | — |
| Depends on | [CC-046](https://github.com/rtumelty/Cathartic-Colours/issues/97) (the `GameDriver`, the automated cases and the runner; its decision D7 keeps them free of editor-only code) |
| Decision | ADR-007, device test automation (written in CC-047.1) |
| Related | [build-android](../systems/build-android.md), [test-plan](../test-plan.md), CC-046's `test-automation` page |

## Goal
Agents can run the test plan **on the phone**, which Ronan named as the primary use case (2026-11-21). Two ways, because they answer different questions:
1. **Automated cases on the device.** The same PlayMode tests CC-046 writes run inside a test build on the phone: real GPU, real file system, real FMOD, real performance. The results come back as an XML report.
2. **An agent operating the real development APK.** It works through the phone's own input (adb taps, swipes and keys), reads the game's state, takes screenshots and goes step by step through any test-plan case. That includes the cases a test build can't reach: installing over an old build, pausing and resuming the app, the back button and real touch.

## Today (verified 2026-11-21)
- **Builds:** `tools/build-android.ps1` makes the development APK (IL2CPP ARM64). Version codes come from CC-039. An unchanged rebuild takes about 1–2 minutes and a clean one about 13 (CC-043).
- **Device:** the CC-042 session installed and timed builds on the Galaxy S23 over USB with `adb` (from Unity's Android SDK), so the connection works.
- **Tests:** CC-046 (in progress) adds the driver and the editor playthroughs. Nothing runs on a device yet.
- **Unity's official route:** the Test Framework can build a player containing the PlayMode tests, install and run it on a connected device, and report results back to the editor ([command-line reference](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/test-framework-introduction/reference-command-line): `-runTests -testPlatform Android`, `-playerHeartbeatTimeout`). Whether the UI Test Framework's simulator and the Input System test devices work inside a player isn't documented. CC-047.1 finds out.

## Approach
- **D2 — Official route for automated cases:**
  - The cases run in a Test Framework player on the device.
  - Custom tooling only fills recorded gaps, as in CC-046.
  - If the UI simulator or the test devices don't work in a player, the fallback is the device driver (D5) for those steps, decided in CC-047.1.
- **D3 — The test build is a separate app on the phone:**
  - A `-testPlatform Android` run would otherwise build with the game's own package id. It could overwrite the installed development build, fail on a lower version code, or touch its save.
  - Test builds get their own id, `com.catharticcolours.game.tests`, and their own name, set by a test-only build modifier. They install side by side, and your installed game and its save are never touched.
- **D4 — A development-build state probe (a game-code change).** For an agent to tap "Start" on the real APK, it needs to know where Start is, and what the board shows afterwards. A small probe compiled **only into development builds** (`DEVELOPMENT_BUILD`) writes, on request:
  - every named UI element's screen rectangle and text, and whether it's visible;
  - the board in test-plan notation, the score, the move count and game over;
  - the last FMOD events played.

  It writes to the app's files folder, as JSON, which `adb` reads (`run-as`, allowed for development builds). It only reads; it never changes the game.
  - **Release builds:** a build check (an EditMode test on the release profile's defines, plus a scan of the release APK for the probe's type name) proves it's absent.
  - This is the one game hook in the whole testing work. That's why it's a decision for you, and the alternative is listed under Options.
- **D5 — A device driver for agents, `tools/device.ps1`, built on adb:**
  - `install` (the newest APK, or a given one), `launch`, `stop`;
  - `state` (calls the probe and prints the JSON);
  - `tap <element name>` (from the probe's rectangles), `tap <x,y>`, `swipe <up|down|left|right>`;
  - `key back|home`, `screenshot <name>`, `logcat`.

  Each call writes into a run folder with the same report format as CC-046. With it, an agent reads a manual case and runs it step by step on the phone.
- **D6 — Runner integration:**
  - `tools/run-playthrough.ps1 -Device` builds the test app and runs the chosen cases on the phone.
  - It pulls their screenshots and the logcat into the same report as an editor run, marked "device".
  - The test plan's `Automated` field says where each case can run (editor, device or both), or names a driver script.
- **D7 — Device-only cases automated in this feature:**
  - **Android build (DROID):** install over the previous build keeping the save (TP-DROID-08), app start time, pause and resume with Home, the back button, rotation if supported.
  - **Touch swipe** with real touch (TP-STD-08).
  - **Save timing** (the CC-042 measurement), as a repeatable device case.
  - **Stays manual:** how audio sounds, and visual and touch feel.
- **D8 — Phone safety:**
  - The scripts never change phone settings, never uninstall the game itself, and only install the development APK or the separate test app.
  - Runs need the phone connected, unlocked and awake. That's your one-time setup (MANUAL 1).
  - If the phone isn't reachable, the run says so and stops; it doesn't wait or retry endlessly.

### Options for D4 (the probe)
- **A — Development-only probe (recommended):** exact element positions and state; small; absent from release builds.
- **B — No game hook:**
  - The agent finds buttons on screenshots by sight and reads the score from the HUD.
  - It needs no game change, but it's fragile, slower, and can't read the board exactly.
- **C — Third-party SDK (AltTester):** a full object-inspection driver, but it adds a server to the build and a licensing decision (GPL or commercial). Only if A falls short.

## Deliverables

### CC-047.1 ([#103](https://github.com/rtumelty/Cathartic-Colours/issues/103)) — Prove the official route on the phone, and ADR-007
**Risk:** High · **Worker:** primary · **Review:** primary
1. A `-testPlatform Android` run of CC-046's smoke tests and a few of its cases on the phone, from the command line, with results XML.
2. Answer these on the device:
   - does `PanelSimulator` drive the panels in a player;
   - do Input System test devices move blocks;
   - do screenshots work;
   - how long does a test build take (clean and unchanged)?
3. The separate test app id (D3); check that your installed game and its save are unchanged afterwards.
4. Write ADR-007: the answers, the gaps, and what fills each.

**Stop rule:** if the Test Framework can't run on the device at all, stop and tell Ronan with the error before building anything custom.

**Definition of Done** (template plus):
- [ ] Device results XML from a command-line run; answers and timings on this page and in ADR-007
- [ ] Installed game and save checked unchanged

### CC-047.2 ([#104](https://github.com/rtumelty/Cathartic-Colours/issues/104)) — Automated cases on the device through the runner
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
- `run-playthrough.ps1 -Device`: builds the test app, runs the chosen cases, and pulls screenshots and logcat into the report.
- The `Automated` field gains editor, device or both.
- Run every CC-046 case on the phone. A case that passes in the editor but fails on the device becomes a bug issue and is skipped on the device with the issue number.

**Definition of Done:**
- [ ] `-Device -All` produces a report on the phone; differences filed as issues

### CC-047.3 ([#105](https://github.com/rtumelty/Cathartic-Colours/issues/105)) — Development-build state probe
**Risk:** High · **Worker:** primary (game code that must never reach a release build) · **Review:** primary
- The probe (D4) in Presentation, wrapped in `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. Requests come over adb, as an `am broadcast` or a request file; ADR-007 picks one.
- EditMode tests for the JSON content. A release-profile check that the probe is compiled out.

**Definition of Done:**
- [ ] Probe output matches the driver's board readout for the same game; release check passes

### CC-047.4 ([#106](https://github.com/rtumelty/Cathartic-Colours/issues/106)) — Device driver and device-only cases
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary
- `tools/device.ps1` (D5), with the same run-folder report.
- Driver scripts for the D7 cases, runnable with `run-playthrough.ps1 -Device -Case TP-DROID-08`.
- An agent guide on the test-automation page: how to run any manual case on the phone step by step, and how to judge screenshots.

**Definition of Done:**
- [ ] D7 cases run from the command line and pass, or are filed as bugs
- [ ] Guide written; an agent run of one manual case recorded as the example

### CC-047.5 ([#107](https://github.com/rtumelty/Cathartic-Colours/issues/107)) — Wrap-up and PR
**Risk:** Low · **Worker:** Haiku · **Review:** primary
- Test plan, index and log.
- The project `CLAUDE.md` §4 device commands (a schema change approved with this plan).
- Device build at the PR, with the probe present in development and absent in release.
- PR and quiz.

## Workflow exception (Ronan, 2026-11-21)
- **Starts when CC-046 merges.** The sandbox runs one feature session at a time, and this builds on CC-046's driver.
- No approval stops between deliverables, as CC-046.
- **Needs the phone:**
  - The session checks the phone is connected before each device step.
  - If it isn't, it records that, does the work that doesn't need the phone, and tells you over Remote Control what's waiting.
- **Stop and tell Ronan** if the CC-047.1 stop rule triggers, if the probe needs more than read-only access to the game, or if scope or decisions D2 to D8 would change.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-047.1 | High | primary | Spike on the device; sets the pattern |
| CC-047.2 | Medium | Sonnet | Runner extension against the .1 findings |
| CC-047.3 | High | primary | Game code; must never ship in release |
| CC-047.4 | Medium | Sonnet | Script against a clear spec |
| CC-047.5 | Low | Haiku | Docs, build and PR |

## MANUAL (Ronan)
1. **Before the session starts (one-time, on the phone):**
   - USB debugging on, and this computer authorised.
   - **Settings → Developer options → Stay awake** on, so the screen stays on while charging over USB.
   - No screen lock while you're away, or the lock time set long enough for runs.

   These are phone settings, so they're yours to change.
2. **During PR review:**
   - Run `tools\run-playthrough.ps1 -Device -Area STD` and one driver case (`-Case TP-DROID-08`).
   - Read the reports.
   - Check that your installed game's save survived.

## Decisions
- **D1 — Workflow** (Ronan, 2026-11-21): starts on CC-046's merge, without approval stops.
- **D2 to D8** as in Approach (approved with the plan, 2026-11-21). **D4: option A, the development-only probe** (Ronan, 2026-11-21).

## Divergences
_None yet._

## Feature test plan
1. `run-playthrough.ps1 -Device -Case TP-STD-04` passes on the phone; the report has device screenshots.
2. `device.ps1 launch`, `tap start-button`, `swipe up`, `state`: the board changes as expected.
3. `run-playthrough.ps1 -Device -Case TP-DROID-08`: the install keeps the save.
4. The release APK doesn't contain the probe.

## Status log
- 2026-11-21: Planned and approved by Ronan with the development-only probe (D4 option A); sub-issues #103-#107; starts when CC-046 merges
