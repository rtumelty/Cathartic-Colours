# CC-003 — FMOD project clean-up and Studio 2.04 migration

| Field | Value |
|---|---|
| Issue | [#9](https://github.com/rtumelty/Cathartic-Colours/issues/9) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-003-fmod-cleanup` (from `develop`, created by the planning session on approval) |
| PR | [#66](https://github.com/rtumelty/Cathartic-Colours/pull/66) |
| Depends on | [CC-033](https://github.com/rtumelty/Cathartic-Colours/issues/45) (FMOD for Unity 2.04, done) |
| Blocks | [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10) (Android builds use the Mobile banks; both edit `CLAUDE.md` and the audio docs) |

## Goal
The FMOD Studio project in `Cathartic Colours - FMOD/` is a clean, saved FMOD Studio 2.04 project. Git tracks only what FMOD Studio needs to reopen it and what Unity needs to play sound. Agents can rebuild the Desktop and Mobile banks from the command line, and the audio docs describe the project as it is now.

## Scope
**In**
- Stop tracking FMOD Studio's per-machine folders `.cache/` (233 files) and `.user/` (3 files), and add a `.gitignore` in `Cathartic Colours - FMOD/` for `.cache/`, `.user/` and `.unsaved/`.
- Keep the built banks in `Build/Desktop/` and `Build/Mobile/` committed (D1).
- A bank build script `tools/build-fmod-banks.ps1` around `fmodstudiocl.exe -build` (D2).
- Check the Mobile platform encoding settings (moved here from CC-004): FADPCM at 24 kHz, Desktop Vorbis quality 37 (D3).
- Rewrite [audio-fmod](../systems/audio-fmod.md) for the saved 2.04 project; update project `CLAUDE.md` §1 (FMOD Studio version) and §5 (bank builds); add test-plan cases.

**Already done by Ronan on `develop` before work started**
- Banks rebuilt in FMOD Studio 2.04 (commit 576f6cb; the banks came out byte-identical, only `.cache` build records changed).
- Project re-saved in FMOD Studio 2.04 (commit 96298b1). The `.fspro` reads `Studio.02.04.00`, the Mobile platform `{f18948da-…}` and its encoding setting are now in `Metadata/`, and `.unsaved/` is gone from git. This replaces the MANUAL Save step in the approved plan.

**Out (they become backlog items if found)**
- Sound design changes, new events, mixing or bank layout changes.
- The 134 metadata files that still carry `serializationModel="Studio.02.03.00"`. FMOD Studio rewrites a file only when it changes, so a mixed project is normal. Don't touch them by hand.
- Android builds and device testing ([CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10)).

## Approach
- **Unity reads banks from the Studio project.** `Assets/Resources/FMOD/FMODStudioSettings.asset` has `HasSourceProject: 1`, `sourceBankPath: ../Cathartic Colours - FMOD/Build` and `ImportType: 0` (StreamingAssets). FMOD for Unity copies the banks into the player at build time. That is why `Build/` stays in git (D1).
- **Untrack, don't delete.** `git rm -r --cached` on `.cache/` and `.user/`. The files stay on disk for the FMOD Studio that may be open, and FMOD Studio regenerates them when they're missing.
- **The command-line build must not run against a project open in FMOD Studio.** Agents run it in the sandbox copy only (`CLAUDE.local.md`). The script fails early with a clear message if `fmodstudiocl.exe` isn't at the FMOD Studio 2.04.00 default path, and takes `-FmodStudioCl <path>` to override.
- **Follow existing patterns:** PowerShell scripts in `tools/` (like `tools/roadmap-import.ps1`), logs in the scratchpad, never in the repo.

## Deliverables

### CC-003.1 — Untrack FMOD Studio's per-machine folders
**Sub-issue:** [#58](https://github.com/rtumelty/Cathartic-Colours/issues/58) · **Risk:** Low · **Worker:** Haiku · **Review:** primary
1. Check the saved project first: `Metadata/Platform/` has both Desktop and Mobile, `Metadata/EncodingSetting/` has two settings, `Metadata/Event/` has the five events, and `git ls-files "Cathartic Colours - FMOD/.unsaved"` is empty. Stop and report if any check fails.
2. Add `Cathartic Colours - FMOD/.gitignore` with `.cache/`, `.user/` and `.unsaved/`.
3. `git rm -r --cached` `.cache/` and `.user/`. Confirm with `git status` that only those paths are staged as deleted and that the files are still on disk.
- **CC-003.2** (worker Sonnet, as planned; primary reworded two messages and ran the clean-checkout test):
  - `tools/build-fmod-banks.ps1` with `-Platforms`, `-Diagnostic`, `-FmodStudioCl`, `-LogDir`. Logs go to `%TEMP%\fmod-bank-build-<timestamp>`. A missing tool exits 1 with the install/override message. The tool reports `Version 2.04.00, Build #168298`.
  - `-Diagnostic`: "Project validation completed successfully", no errors or warnings.
  - FMOD skips banks whose `.cache/` build records are current, so in the sandbox (which has `.cache/`) the script wrote nothing. In a fresh worktree of the branch (no `.cache/` or `.user/`) it rebuilt Desktop and Mobile, and all four banks were **byte-identical** to the committed ones (Desktop `Master.bank` 88,512 bytes, Mobile `Master.bank` 174,912 bytes, strings banks 972 bytes). `git status` showed only the ignored, regenerated `.cache/`; no `.user/` was created. No banks committed.

**Definition of Done**
- [ ] The three checks above pass, with output in the commit review
- [ ] `git ls-files "Cathartic Colours - FMOD"` lists only `.gitignore`, the `.fspro`, `Assets/`, `Build/` and `Metadata/`
- [ ] A batchmode open of the Unity project (`-batchmode -quit -projectPath "Cathartic Colours"`) logs no FMOD bank errors
- [ ] Wiki log entry; sub-issue status set on the board
- [ ] Reviewed by primary; committed per user-level §4

### CC-003.2 — Bank build script
**Sub-issue:** [#59](https://github.com/rtumelty/Cathartic-Colours/issues/59) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. `tools/build-fmod-banks.ps1`: runs `fmodstudiocl.exe -build` on `Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro`, with optional `-Platforms` (default: all). `-log-dir` points at a temp folder. The script returns the tool's exit code and prints the paths of the banks it wrote.
2. Run it once with `-diagnostic` too, and record the result on this page.
3. Rebuild the banks and compare them with the committed ones. If they differ, commit the rebuilt banks only when the difference is explained (for example, a 2.04 format change), and record it.

**Definition of Done**
- [ ] The script builds Desktop and Mobile banks from a clean checkout of this branch
- [ ] The rebuilt banks match the committed ones, or the difference is explained on this page
- [ ] `-diagnostic` reports no project errors (warnings recorded)
- [ ] No files outside `Build/` change when the script runs (no `.cache/` or `.user/` appear in `git status`)
- [ ] Reviewed by primary; committed per user-level §4

### CC-003.3 — Audio docs and test plan
**Sub-issue:** [#60](https://github.com/rtumelty/Cathartic-Colours/issues/60) · **Risk:** Low · **Worker:** Sonnet · **Review:** primary
1. Rewrite [audio-fmod](../systems/audio-fmod.md): the project layout (saved 2.04 project, Desktop and Mobile platforms and their encodings, what's tracked and ignored), the bank build script, and how Unity picks the banks up. Remove the "`.unsaved/` hazard" section and its resolved note.
2. Project `CLAUDE.md` §1: the FMOD Studio project is 2.04. §5: replace the "migrate to 2.04 before rebuilding banks" note with the bank script command, and keep the "save before building or committing" rule.
3. Test plan, area Audio: add a case that the editor plays all five events after a clean checkout, and an "Editor and tooling" case for the bank script.
4. Wiki index and log entries.

**Definition of Done**
- [ ] No page still says the project is 2.03 or that events live in `.unsaved/` (grep the wiki and `CLAUDE.md`)
- [ ] New test cases follow the test plan's format and order
- [ ] Reviewed by primary; committed per user-level §4

## Tests
No game code changes, so no unit tests. System checks:
- The batchmode Unity open after the untracking (CC-003.1).
- The bank rebuild from the script, compared with the committed banks (CC-003.2).
- MANUAL step 1 below: the editor plays sound, and FMOD Studio reopens the project cleanly.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-003.1 | Low | Haiku | Mechanical git changes; the primary checks the saved-project checks first |
| CC-003.2 | Medium | Sonnet | Script and bank comparison; the primary judges any bank difference |
| CC-003.3 | Low | Sonnet | Doc rewrite needs the facts from .1 and .2; primary review |

## MANUAL (Ronan)
1. **After CC-003.2: reopen and listen.** The agent asks first whether to switch the manual copy (`Cathartic-Colours_2`) to `feature/CC-003-fmod-cleanup`.
   - Open `Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro` in FMOD Studio 2.04. Confirm there's no migration prompt and that Desktop and Mobile both still appear in the platform settings, where you added Mobile. Close it without saving if nothing changed.
   - Open the Unity project, open the main menu scene, press Play, and play until a move, a small merge and game over have happened. Each should play its sound, with no FMOD errors in the Console.

## Decisions
- **D1 — Banks stay committed.** Unity builds and plays sound from a clean checkout without FMOD Studio installed. The banks are small (Desktop about 89 KB, Mobile about 175 KB). Rebuild them with the script and commit them whenever the Studio project changes.
- **D2 — Command-line bank builds.** `fmodstudiocl.exe` ships with FMOD Studio 2.04.00 on Ronan's machine, so agents can rebuild banks after a Studio change without opening the GUI.
- **D3 — Mobile encoding kept as Ronan set it.** FADPCM at 24 kHz is FMOD's usual low-CPU choice for mobile one-shots; Desktop stays Vorbis quality 37. Revisit in CC-017 (performance) if needed.

## Divergences
- 2026-11-19 (planning session, before work started): Ronan's re-save on `develop` (96298b1) did the MANUAL Save step and the `.unsaved/` removal from the approved plan. The plan's first deliverable is reduced to untracking `.cache/` and `.user/`.

## Implementation notes
- **CC-003.1** (worker Haiku, as planned): saved-project checks passed: `Metadata/Platform/` has Desktop `{37b3f97d-…}` and Mobile `{f18948da-…}`; `Metadata/EncodingSetting/` has Vorbis quality 37 and FADPCM 24 kHz; `Metadata/Event/` has 5 events; nothing under `.unsaved/` tracked. 236 files untracked (233 `.cache/`, 3 `.user/`), still on disk.
- **CC-003.2** (worker Sonnet, as planned; primary reworded two messages and ran the clean-checkout test):
  - `tools/build-fmod-banks.ps1` with `-Platforms`, `-Diagnostic`, `-FmodStudioCl`, `-LogDir`. Logs go to `%TEMP%\fmod-bank-build-<timestamp>`. A missing tool exits 1 with the install/override message. The tool reports `Version 2.04.00, Build #168298`.
  - `-Diagnostic`: "Project validation completed successfully", no errors or warnings.
  - FMOD skips banks whose `.cache/` build records are current, so in the sandbox (which has `.cache/`) the script wrote nothing. In a fresh worktree of the branch (no `.cache/` or `.user/`) it rebuilt Desktop and Mobile, and all four banks were **byte-identical** to the committed ones (Desktop `Master.bank` 88,512 bytes, Mobile `Master.bank` 174,912 bytes, strings banks 972 bytes). `git status` showed only the ignored, regenerated `.cache/`; no `.user/` was created. No banks committed.
- **MANUAL step 1** (Ronan, 2026-11-19, manual copy on 442e23b): FMOD Studio 2.04 reopened the project with no migration prompt and both platforms present; the editor played the move, merge and game-over sounds with no FMOD errors. Pulling CC-003.1 into a copy that had the old tracked `.user/` files deleted them from disk, so Ronan's layout was restored from a backup (now ignored). Other copies will see the same on their first pull.
- **CC-003.3** (worker Sonnet, as planned): audio-fmod rewritten (2.04 layout, tracked and ignored folders, platforms and encodings, rebuilding the banks; `.unsaved/` hazard section removed), `CLAUDE.md` §1 and §5, TP-AUDIO-05 rewritten, TP-AUDIO-06 and TP-TOOL-04 added, ADR-003 note, index. Primary review fixed three paths where the worker's script turned `\b` into a backspace character, and a stale Android line.
  - Finding for [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10): nothing in FMOD for Unity 2.04 or the committed settings points Android at `Build/Mobile/` (`Platform.cs` line 720 defaults `BuildDirectory` to `Desktop`; the Mobile platform templates set none and are inactive). CC-004 has to check which banks an Android build packs, and may need a `BuildDirectory` override.

## Feature test plan
1. `tools/build-fmod-banks.ps1` on a clean checkout: exit code 0, Desktop and Mobile banks written, `git status` clean apart from any explained bank change.
2. FMOD Studio 2.04 opens the project with no migration prompt; both platforms present.
3. Unity editor: move, merge and game-over sounds play, with no FMOD errors.

Folded into the [test plan](../test-plan.md) as TP-TOOL-04 (case 1), TP-AUDIO-05 (case 2) and TP-AUDIO-06 (case 3). Cases 2 and 3 passed in Ronan's manual check on 2026-11-19; case 1 passed in the agent's fresh-worktree run.

## Status log
- 2026-11-19: Planned
- 2026-11-19: Approved by Ronan (plan, D1–D3 and routing)
- 2026-11-19: CC-003.1 to CC-003.3 approved by Ronan; PR opened to develop
