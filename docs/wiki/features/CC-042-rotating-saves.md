# CC-042 — Rotating save files and reporting bad saves

| Field | Value |
|---|---|
| Issue | [#70](https://github.com/rtumelty/Cathartic-Colours/issues/70) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-042-rotating-saves` (from `develop`, created by the planning session on approval) |
| PR | — |
| Depends on | [CC-031](https://github.com/rtumelty/Cathartic-Colours/issues/43) (atomic write and save checks, done in CC-041) |
| Related | [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25) (cloud saves), [CC-015](https://github.com/rtumelty/Cathartic-Colours/issues/27) (analytics), [save-system](../systems/save-system.md) |

## Goal
A corrupt or unreadable save costs the player at most one move instead of the whole game. Every rejected save is reported with its reason, so a future analytics or crash-reporting hook can pick it up.

## Today (verified on `develop`, 2026-11-20)
- **Writing:** `SaveGameManager.WriteSave` writes `savegame.json` atomically, through `savegame.json.tmp` and `File.Replace`. When `File.Replace` isn't supported, it falls back to delete and move.
- **Unusable saves:** `TryReadSave` checks parsing, `version` and grid size. An unusable file is moved to `savegame.bad.json`, replacing any earlier one, with a `Debug.LogWarning`. `HasSaveGame` is then false and the player loses the game.
- **When saves happen:** `SaveGameSystem` saves once per accepted move. It deletes the save when the game ends. Starting a new game leaves the old save until the new game's first move overwrites it.

## Approach
**Proposed defaults (D2 to D5), approved with the plan:**
- **D2 — Three files:**
  - **Files:** `savegame.json` is the newest and keeps its name, so existing saves still load. `savegame.1.json` and `savegame.2.json` are the two moves before it.
  - **Rotation on each save:** delete `.2`, move `.1` to `.2`, then `File.Replace(tmp, savegame.json, savegame.1.json)`. The replace moves the old newest file into `.1` as part of the same call. Where `File.Replace` isn't supported, the same steps happen as moves.
  - **Crash safety:** a crash at any step leaves at least one complete earlier save.
  - **Cost:** one write and two renames per move, on files of a few KB. Measured on the PR's device build, not assumed.
- **D3 — Load falls back:**
  - `LoadSaveGame` and `HasSaveGame` try the newest file, then `.1`, then `.2`, and use the first one that passes the existing checks.
  - Each rejected file is set aside (D4).
  - When the game resumes from an older file, the backups are renumbered so the next save rotates normally.
- **D4 — Reporting and clean-up:**
  - **Reasons:** a rejection has a reason (`Unreadable`, `Empty`, `UnknownVersion`, `InvalidGrid`) and a detail string.
  - **Hook:** `SaveGameManager` raises a static `SaveRejected` event with the reason, the file and the detail, and logs one warning line in a fixed format. The event is the hook for analytics later (CC-015); nothing subscribes yet.
  - **Kept files:** rejected files go to `savegame.bad.<UTC timestamp>.json`. Only the 3 newest are kept; older ones are deleted when a new one is added.
- **D5 — Player messages and game boundaries:**
  - **No player message** in this feature. A resume that lost a move looks like a normal resume. A message ("Your last move couldn't be restored") would need UI work and your eye on it, so it's a follow-up backlog item if you want it.
  - **New games:** the backups belong to one game. The first save of a new game (move count 1) clears `.1` and `.2` before rotating, so a fallback never loads the previous game.
  - **Game over** deletes all three files and any leftover `.tmp`.
- **Unchanged:**
  - The save format (`version` stays `"1.0"`). Only the file layout changes.
  - Cloud saves (CC-013) will sync only the newest valid save; the backups stay on the device. That's noted on the save-system page for CC-013.
- **Assembly:** Core (`SaveSystem`), with EditMode tests in `CatharticColours.Core.Tests` using the existing `SaveFilePathOverride`.

## Deliverables

### CC-042.1 — Rotating saves with fallback on load
**Sub-issue:** [#87](https://github.com/rtumelty/Cathartic-Colours/issues/87) · **Risk:** High · **Worker:** primary (save data, hard to reverse on players' devices) · **Review:** primary
1. **Rotation** in `WriteSave`, with the `File.Replace` fallback path. Clear the backups on a new game's first save, and delete all the files on game over.
2. **Fallback** load in `TryReadSave`, `LoadSaveGame` and `HasSaveGame`.
3. Update the [save-system](../systems/save-system.md) page and the TP-SAVE cases.

**Tests (EditMode):**
- **Rotation:** after 1, 2, 3 and 4 saves, the newest is in `savegame.json` and only two backups exist.
- **Fallback:** a corrupt, truncated or unknown-version newest file loads `.1`, and when both are bad it loads `.2`.
- **No valid file:** `HasSaveGame` is false.
- **Game boundaries:** a new game's first save clears the backups, and game over removes every file.
- **Compatibility:** an existing single `savegame.json` still loads.
- **Crash leftovers:** a leftover `.tmp` doesn't block a later save.
- **Real save:** tests never touch the real save (sentinel check, as in CC-002.3).

**Definition of Done** (template plus):
- [ ] Every test above passes; full EditMode and PlayMode runs pass from the command line
- [ ] save-system page and test plan updated

### CC-042.2 — Rejection reasons, report event and bad-file clean-up
**Sub-issue:** [#88](https://github.com/rtumelty/Cathartic-Colours/issues/88) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. A `SaveRejectReason` enum and a `SaveRejected` event on `SaveGameManager`. `Validate` returns a reason instead of a string, and the log line uses one fixed format.
2. Timestamped bad files, capped at 3.
3. Update the save-system page: the reporting hook and how CC-015 should subscribe.

**Tests (EditMode):**
- the event fires once per rejected file, with the right reason;
- no event fires for a valid save;
- the fourth bad file removes the oldest;
- the log line matches its format (`LogAssert`).

**Definition of Done:**
- [ ] Tests pass; full EditMode and PlayMode runs pass
- [ ] save-system page updated

### CC-042.3 — PR, device build and wrap-up
**Sub-issue:** [#89](https://github.com/rtumelty/Cathartic-Colours/issues/89) · **Risk:** Low · **Worker:** Haiku · **Review:** primary
- Feature test plan folded into `test-plan.md`.
- **Device build at the PR** (user-level §2.3).
- PR and quiz.

Optional, if you want the player told: create a backlog issue for the resume message (D5).

## Workflow exception (Ronan, 2026-11-20)
The same as CC-041:
- No approval stops between deliverables, and no manual checks. The session works straight through and opens one PR at the end.
- Each sub-issue goes to In progress when work starts, and to In review when its commit is pushed. The planning session closes them after the merge.
- Long builds don't hold up commits.
- A divergence that changes scope or a decision above: stop, record it on this page, and tell Ronan over Remote Control.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-042.1 | High | primary | Save layout on players' devices |
| CC-042.2 | Medium | Sonnet | Contained, against a clear spec |
| CC-042.3 | Low | Haiku | Docs, build and PR |

## MANUAL (Ronan)
None during the work. On your return, during PR review: TP-SAVE device cases. Play a few moves, quit, resume. Optionally corrupt `savegame.json` with `adb` and check that the game resumes one move earlier.

## Decisions
- **D1 — No-stops workflow** (Ronan, 2026-11-20).
- **D2 to D5** as in Approach (proposed by planning; approved with the plan).
- **D6 — Runs in its own git worktree, in parallel with CC-039 in the sandbox** (Ronan, 2026-11-20). The two features touch different files. The worktree has no Unity Library at first, so its first batchmode run imports the project.

## Divergences
Implementation specifics only; no change to scope or to D2 to D5.
- **CC-042.1, new game's first save:** the plan said it "clears `.1` and `.2` before rotating". Rotating would then move the old game's `savegame.json` into `.1`, which D5 rules out. So the first save deletes `.1` and `.2` and replaces `savegame.json` without keeping a backup (`File.Replace` with no backup name). The new game has backups from its second move on.
- **CC-042.1, test seam:** `SaveGameManager.SimulateNoFileReplace` (internal, test-only, like `SaveFilePathOverride`) makes `WriteSave` take the path used where `File.Replace` isn't supported, so both paths are tested.
- **CC-042.1, real-save check:** the editor's real save folder (`%USERPROFILE%\AppData\LocalLow\Ronan Tumelty\Cathartic Colours`) had no `savegame*.json` before the runs; checked again after each run instead of planting a sentinel file there, since the manual copy shares that folder.

## Feature test plan
1. Play three moves, quit to the menu, resume: the board is as left.
2. Corrupt `savegame.json` (device: `adb shell run-as com.catharticcolours.game`, then edit the file under `files/`), restart the app, resume: the board is one move earlier, and a `savegame.bad.*.json` file exists.
3. Finish a game: no save files remain, and the menu shows no Resume.

## Status log
- 2026-11-20: Planned and approved by Ronan (no-stops workflow)
