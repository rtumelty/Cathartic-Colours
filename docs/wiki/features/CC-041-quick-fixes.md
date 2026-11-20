# CC-041 — Quick-fix batch

| Field | Value |
|---|---|
| Issue | [#75](https://github.com/rtumelty/Cathartic-Colours/issues/75) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-041-quick-fixes` (from `develop` after CC-002 merged, created by the planning session) |
| PR | — |
| Depends on | [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8) (Core and Presentation assemblies, done) |

## Goal
Fix the eleven small open bugs and chores in the Quick fixes phase in one branch: one commit per issue, and one pull request at the end.

## Workflow exception (Ronan, 2026-11-19)
- **No approval stops between fixes and no manual checks.** The session works straight through the list and opens the PR when every fix is in. Ronan reviews the whole PR and merges it as usual.
- Each fix is its own commit, titled `CC-0nn: <what it fixes>`, readable on its own (user-level `CLAUDE.md` §4).
- Each sub-issue goes to In progress when its fix starts and In review when its commit is pushed. The planning session closes them after the merge (develop isn't the default branch, so `Closes` lines don't fire).
- The agent edits UXML, USS and prefab YAML itself, which project `CLAUDE.md` §5 would normally make a manual step, and checks each such change with a batchmode compile and scene load.
- Long builds don't hold up commits (user-level §2.2).
- Divergences that change scope still need Ronan: if a fix turns out bigger than its issue describes, or needs a design decision, stop that fix, record it and move on to the next.

## Scope
**In, in this order** (each is a sub-issue of #75):

| # | Issue | Fix | Risk / worker |
|---|---|---|---|
| 1 | [CC-040](https://github.com/rtumelty/Cathartic-Colours/issues/69) | Let a build re-save the five settings files, check the diffs only add default fields, commit them, and remove the revert step from `tools/build-android.ps1` and [build-android](../systems/build-android.md) | Low / Haiku |
| 2 | [CC-037](https://github.com/rtumelty/Cathartic-Colours/issues/64) | `GameInputHandler`: drop the plain `<Keyboard>/upArrow` binding; cache or dispose the per-move entity query | Low / Sonnet |
| 3 | [CC-038](https://github.com/rtumelty/Cathartic-Colours/issues/65) | Remove the per-move "Game saved" and "Save game loaded" info logs; keep the error logs | Low / Haiku |
| 4 | [CC-031](https://github.com/rtumelty/Cathartic-Colours/issues/43) | Write the save to a temp file and rename it into place; read `version` and reject unknown or unreadable files (renamed to `savegame.bad.json`, no Resume button); restore `colorProfileName` when that profile exists | **High / primary** (save format) |
| 5 | [CC-044](https://github.com/rtumelty/Cathartic-Colours/issues/77) | Restore the random generator state directly (`new Random { state = … }`, guard a zero state); flip the two save tests that pin the old behaviour | Low / Sonnet |
| 6 | [CC-025](https://github.com/rtumelty/Cathartic-Colours/issues/37) | Guards: null colour profile in `GameManager.Initialize`; scene name matches `Gameplay scene.unity`; `GameRenderer.CleanUp` skips entity work when the world is gone or was never set up | Medium / Sonnet |
| 7 | [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40) | `GameUI.uxml`: add `status-label`; give the two `restart-button`s unique names and hook each once; keep handler references so `UnhookEvents` works | Medium / Sonnet |
| 8 | [CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41) | Destroy grid cell objects in `CleanUp`; tint the cell's `MeshRenderer` (MaterialPropertyBlock) with `ColorProfile.gridBackgroundColor` | Medium / Sonnet |
| 9 | [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39) | `LoadPreset` copies tier points; the menu exposes the spawn toggle; resuming uses its own configuration copy so the menu keeps its settings | Medium / Sonnet |
| 10 | [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44) | `JellyManager` uses a per-instance material and frame-rate-independent smoothing; delete `JellyController`; fix `ScoreParticles.prefab` stale fields and empty digit systems; top-tier clear particles get a visible colour; remove the unused `wobbleActive` (CS0414) | Medium / Sonnet |
| 11 | [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42) | Input reads `GameControls.inputactions` (keyboard and gamepad); mouse drag works as a swipe in the editor; one dead-zone rule for keyboard and swipe; `SwipeDetector`'s namespace stays (prefab reference) | Medium / Sonnet, last |

**Out**
- [CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31) (level-complete panel) waits for puzzle mode; [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38) (rule gaps) is design input for CC-008. Both are in the Gameplay phase.
- [CC-042](https://github.com/rtumelty/Cathartic-Colours/issues/70) (rotating saves, bad-save reporting), [CC-043](https://github.com/rtumelty/Cathartic-Colours/issues/76) (slow builds), [CC-045](https://github.com/rtumelty/Cathartic-Colours/issues/79) (URP references).
- New finds become backlog issues, not part of this branch.

## Approach
- Follow [ADR-004](../decisions/ADR-004-core-presentation-assemblies.md): fixes in Core code get EditMode tests in `CatharticColours.Core.Tests`; Presentation fixes are covered by PlayMode tests where a scene test is practical, and by the test plan otherwise.
- Tests written in CC-002 that pin current buggy behaviour (they name their issue) are flipped in the same commit as the fix.
- Behaviour that isn't part of an issue stays the same. Every commit leaves the game playable.

## Definition of Done (each fix)
- [ ] 0 compile errors and no new warnings (CC-032 removes CS0414, so the warning count should drop)
- [ ] Full EditMode suite passes from the command line; new or flipped tests for Core changes
- [ ] UXML, USS or prefab changes: batchmode scene load shows no missing references
- [ ] Affected system pages and test-plan cases updated in the same commit
- [ ] Sub-issue set to In review on the board; committed and pushed per user-level §4

## Definition of Done (batch)
- [ ] All eleven fixes committed, or stopped with a recorded reason
- [ ] EditMode and PlayMode runs pass from the command line
- [ ] One Android development build (`tools/build-android.ps1`) succeeds, reported after the PR is open if it's still running
- [ ] An editor or PlayMode run's log has no up-arrow `InvalidOperationException` and no per-move save lines
- [ ] Feature test plan written and folded into [test-plan.md](../test-plan.md)
- [ ] PR into `develop` listing every fix, with `Closes #75` and a `Closes` line per sub-issue; knowledge-check quiz (user-level §2.3)

## Risk and model routing
See the Scope table. The primary model reviews every worker's output before its commit and does CC-031 itself.

## MANUAL (Ronan)
None during the batch (workflow exception). Ronan's review of the PR and his merge are the only checks.

## Decisions
- **D1 — Batch workflow without stops** (Ronan, 2026-11-19), as above.
- **D2 — Unusable saves are kept** as `savegame.bad.json` and ignored, with no Resume button (Ronan, 2026-11-19). Rotating saves and reporting are CC-042.
- **D3 — CC-044 joins the batch** after CC-031, both touching the save restore code (Ronan, 2026-11-20).

## Divergences
- **CC-040:** `tools/build-android.ps1` had no revert step to remove; the revert was only advice on the [build-android](../systems/build-android.md) page, which is updated. A build re-saved three files with real changes (new default fields, plus URP 17.6 dropping `m_DebugLevel`); the other three it touched differed only in line endings, so nothing of theirs is committed.

## Feature test plan
_Written at the end of the batch: the shortest pass that checks each fix in the editor and on a device._

## Status log
- 2026-11-19: Planned and approved by Ronan
- 2026-11-20: CC-044 added; branch cut after CC-002 merged
