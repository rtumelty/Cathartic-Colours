# CC-039 — Android version codes that never go backwards

| Field | Value |
|---|---|
| Issue | [#68](https://github.com/rtumelty/Cathartic-Colours/issues/68) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-039-version-codes` (from `develop`, created by the planning session on approval) |
| PR | — |
| Related | [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10) decision D2 (superseded here), [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57) (store release confirms the scheme), [build-android](../systems/build-android.md) |

## Goal
A build from `develop` always has a higher Android version code than any feature-branch build made before that branch merged, so a new build always installs over an old one on the test phone and keeps the saved game.

## Problem (from the issue)
`tools/build-android.ps1` uses `git rev-list --count HEAD`. Pull requests are squash-merged, so a feature branch with 8 commits counts 8 ahead of `develop`, but adds only 1 to `develop` when merged. The next `develop` build has a lower code, and Android refuses it (`INSTALL_FAILED_VERSION_DOWNGRADE`).

## Approach
**Scheme (D2, proposed):** `versionCode = base × 100 + ahead`.
- `base` is the commit count of the newest `develop` commit that the build contains: `git rev-list --count $(git merge-base HEAD origin/develop)`.
- `ahead` is how many commits the build is past `develop`: `git rev-list --count origin/develop..HEAD`. It's 0 on `develop` itself.
- **Why it works:** a squash merge adds at least 1 to `base`, which is worth 100, and that's more than any feature branch's `ahead` (at most 99). So every `develop` build after a merge beats every build from the merged branch. Rebasing or merging `develop` into a branch also raises its `base`.
- **Limits:**
  - If `ahead` reaches 100, the build fails with a clear message. A branch that long should rebase or merge `develop`.
  - Google Play's maximum is 2,100,000,000, so `base` can reach 21 million commits.
  - Today's codes jump from about 90 to about 11,000. That's higher than anything on the phone, so no uninstall is needed.
- **Known trade-off:** two different branches cut from the same `develop` commit can produce the same code. That's fine for development builds, which are installed one at a time. Store builds come from `master` and are settled in CC-036.
- **Fetching `develop`:** the script runs `git fetch origin develop` first, so `base` is current. Offline, it uses the local `origin/develop` and warns. Without that ref it fails with a message. `-VersionCode <n>` still overrides everything.
- **Testability, following the existing pattern:** the git calls stay in `build-android.ps1`. It passes `-versionBase <n> -versionAhead <n>` to Unity, and the composition and its limits live in `AndroidBuildArgs` (C#), so EditMode tests cover them like the current version-code parsing. `-versionCode` stays as the direct override.
- `build-android.ps1 -PrintVersionCode` prints `base`, `ahead` and the code, then exits without starting Unity. It's a quick check that needs no build.

## Deliverables

### CC-039.1 — Compose the version code from develop's count and commits ahead
**Sub-issue:** [#85](https://github.com/rtumelty/Cathartic-Colours/issues/85) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. `AndroidBuildArgs`:
   - parse `-versionBase` and `-versionAhead`;
   - `ComposeVersionCode(base, ahead)` with the limits above: `ahead` at most 99, the result at most 2,100,000,000, both values at least 0;
   - `-versionCode` wins when it's given.
2. `build-android.ps1`:
   - fetch `develop`;
   - work out `base` and `ahead`;
   - pass them on, or fail with a clear message;
   - add `-PrintVersionCode`;
   - keep `-VersionCode` and `-SkipVersionCode`.
3. Check by running `-PrintVersionCode` in the sandbox on the feature branch, and in a temp worktree on `develop`. Record both outputs on this page.

**Tests:** EditMode tests in `CatharticColours.Build.Editor.Tests` for parsing, composition, both limits, the override and missing arguments (exit code 2).

**Definition of Done** (template plus):
- [ ] `-PrintVersionCode` outputs on `develop` and on the branch recorded; the branch's code is below what `develop` will have after the merge
- [ ] EditMode suite passes from the command line

### CC-039.2 — Docs and the PR build
**Sub-issue:** [#86](https://github.com/rtumelty/Cathartic-Colours/issues/86) · **Risk:** Low · **Worker:** Haiku · **Review:** primary
1. Update [build-android](../systems/build-android.md):
   - the scheme and the reason for it;
   - the limits;
   - the `-PrintVersionCode` and fetch behaviour.

   Also add a note to the CC-004 feature page: decision D2 is superseded by CC-039.
2. Test plan: a TP-DROID case to install a `develop` build over a feature-branch build with no downgrade error, keeping the save.
3. **Device build at the PR** (user-level §2.3): one development APK, built with the new code.

**Definition of Done:**
- [ ] Docs, test plan, index and log updated
- [ ] APK built; its version code from the log recorded in the PR

## Workflow exception (Ronan, 2026-11-20)
The same as CC-041:
- No approval stops between deliverables, and no manual checks. The session works straight through and opens the PR when both deliverables are in.
- Each sub-issue goes to In progress when work starts, and to In review when its commit is pushed. The planning session closes them after the merge.
- A divergence that changes scope or needs a design decision: stop, record it on this page, and tell Ronan over Remote Control.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-039.1 | Medium | Sonnet | Build tooling; hard to get wrong once tested, but every later build depends on it |
| CC-039.2 | Low | Haiku | Docs and the PR build |

## MANUAL (Ronan)
None during the work. On your return, during PR review: install the PR's APK over the current one on the phone (TP-DROID case). It should install without uninstalling first.

## Decisions
- **D1 — No-stops workflow** (Ronan, 2026-11-20), as above.
- **D2 — Scheme `base × 100 + ahead`** (proposed by planning; approved with the plan).

## Divergences
No scope or design changes. Implementation specifics (CC-039.1):
- **Codes start near 7,900, not 11,000.** `develop` had 79 commits when this was built, so its code is 7,900. That's still far above the codes already on the phone (about 90), so no uninstall is needed.
- **The script mirrors the formula.** `-PrintVersionCode` must not start Unity, so `build-android.ps1` also works out `base × 100 + ahead` and rejects `ahead` over 99. The code it prints is for display and the early failure only; Unity gets `-versionBase`/`-versionAhead` and `AndroidBuildArgs.TryComposeVersionCode` is the authoritative check.
- `-PrintVersionCode -VersionCode <n>` prints the override without running git. `-PrintVersionCode -SkipVersionCode` prints that there's nothing to print.
- The three flag readers in `AndroidBuildArgs` share private helpers (`FindFlag`, `TryReadValue`, `TryReadWholeNumber`); `-buildProfile` parsing behaves as before.

## Check results (CC-039.1, 2026-11-20)
| Where | `-PrintVersionCode` output |
|---|---|
| `develop` (`0cc1745`, temp worktree) | base 79, ahead 0, code **7900** |
| `feature/CC-039-version-codes` (1 commit ahead) | base 79, ahead 1, code **7901** |

After this branch merges, `develop` has 80 commits and its code is **8000**, above every build from this branch (7901 up to 7999).

## Feature test plan
1. `tools\build-android.ps1 -PrintVersionCode` on `develop` and on a feature branch: the branch's code is between `develop`'s current code and `develop`'s code after one more commit.
2. Install a `develop` APK over a feature-branch APK on the phone: no downgrade error, and Resume still offers the saved game.

## Status log
- 2026-11-20: Planned and approved by Ronan (no-stops workflow)
