# CC-004 — Android build pipeline (development builds)

| Field | Value |
|---|---|
| Issue | [#10](https://github.com/rtumelty/Cathartic-Colours/issues/10) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-004-android-dev-builds` (from `develop`, created by the planning session on approval) |
| PR | — |
| Depends on | [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9) (FMOD clean-up; merge it first, then merge `develop` into this branch) |
| Blocks | [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57) (store release), [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29) (profiling on device) |

## Goal
An agent can produce an installable Android development build (APK) from the command line with one script. The build has the game's real application id and a version code that grows with every commit, so a newer build always installs over an older one. Ronan can test it on a phone.

## Scope
**In**
- Player settings: Android application id `com.catharticcolours.game` (D1); version name from `bundleVersion` (now `0.0.1`); version code from the commit count (D2).
- The "Android™ Development" build profile builds an APK and doesn't wait for a managed debugger at launch (D3).
- A command-line build: an editor build class in its own assembly with an `-executeMethod` entry point, a `tools/build-android.ps1` wrapper, and EditMode tests for its pure logic (D4).
- One development build, installed on a connected phone with Ronan's yes; a build wiki page, the command in project `CLAUDE.md` §4, and test-plan cases.

**Out**
- Release signing, the keystore, store App Bundles and Google Play ([CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57), Ronan, 2026-11-19). The "Android™" release profile stays as it is.
- FMOD Mobile encoding (moved to [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9)).
- Device-specific bug fixes, performance and UI scaling. They become backlog items if found.
- CI builds.

## Approach
- **Current settings** (`ProjectSettings/ProjectSettings.asset`): `applicationIdentifier` has only `Standalone: com.DefaultCompany.2D-URP`; `bundleVersion: 0.0.1`; `AndroidBundleVersionCode: 9`; `AndroidMinSdkVersion: 26`; target SDK 0 (highest installed); IL2CPP (`scriptingBackend Android: 1`); ARM64 only (`AndroidTargetArchitectures: 2`). Keep all except the id, and set the version code per build.
- **Build profiles** (`Assets/Settings/Build Profiles/`): "Android™ Development" has `m_Development: 1`, `m_AllowDebugging: 1`, `m_WaitForManagedDebugger: 1` and `m_BuildAppBundle: 1`. Change the last two to 0 (D3). Keep development and script debugging on.
- **Build class** in `Assets/Editor/Build/` with its own Editor-only assembly definition `CatharticColours.Build.Editor`, following the `CatharticColours.Simulation` pattern from CC-006, so EditMode tests can reference it. Entry point `CatharticColours.Build.AndroidBuild.Development`. It loads the profile asset, sets the version code from a `-versionCode <n>` argument, and builds with `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)`. It exits non-zero on failure, with the build report's errors in the log.
- **Pure logic, unit-tested:** argument parsing (missing or non-numeric `-versionCode`) and the output file name `CatharticColours-<versionName>-<versionCode>-dev.apk`. Unity calls stay in a thin outer method.
- **Wrapper** `tools/build-android.ps1`: computes the version code with `git rev-list --count HEAD`, runs Unity in batchmode with `-executeMethod`, writes the log to a temp folder, and prints the APK path. Output goes to `Cathartic Colours/Builds/Android/`, which `/[Bb]uilds/` in the Unity `.gitignore` already ignores.
- **Where it runs:** the agent sandbox only (`CLAUDE.local.md`); the build needs the project closed in the editor there.

## Deliverables

### CC-004.1 — Application id and development profile
**Sub-issue:** [#61](https://github.com/rtumelty/Cathartic-Colours/issues/61) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. Set the Android application id to `com.catharticcolours.game` (Player Settings), through a batchmode editor call or a minimal YAML edit verified by a batchmode open.
2. In "Android™ Development", set Build App Bundle off and Wait For Managed Debugger off. Same method; if the profile asset can't be changed safely from code, stop and make it a MANUAL step (Build Profiles window → Android™ Development → untick **Build App Bundle (Google Play)** and **Wait For Managed Debugger**).

**Definition of Done**
- [ ] `ProjectSettings.asset` has `Android: com.catharticcolours.game`; no other setting changes in the diff apart from Unity's re-serialisation
- [ ] The profile diff shows only the two fields changing
- [ ] Batchmode open: no new errors or warnings
- [ ] Wiki log entry; sub-issue status set on the board
- [ ] Reviewed by primary; committed per user-level §4

### CC-004.2 — Command-line development build
**Sub-issue:** [#62](https://github.com/rtumelty/Cathartic-Colours/issues/62) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. The build assembly, build class and EditMode test assembly as in Approach.
2. `tools/build-android.ps1` as in Approach.
3. Run the EditMode tests and one build.

**Definition of Done**
- [ ] EditMode tests cover argument parsing and the file name; the full EditMode suite passes from the command line
- [ ] The script produces an APK whose version code equals the commit count and whose package is `com.catharticcolours.game` (check with `aapt dump badging` or `apkanalyzer` from the Android SDK that Unity installed)
- [ ] A failed build (for example, a missing `-versionCode`) exits non-zero with a clear message
- [ ] No new compiler warnings
- [ ] Reviewed by primary; committed per user-level §4

### CC-004.3 — Device install and docs
**Sub-issue:** [#63](https://github.com/rtumelty/Cathartic-Colours/issues/63) · **Risk:** Low · **Worker:** Haiku · **Review:** primary
1. Ask Ronan whether a phone is connected and whether to install. On yes: `adb install -r <apk>` using the `adb` from the Unity-installed SDK, and report the result.
2. New system page `systems/build-android.md`: settings, profiles, version scheme, script usage, outputs, and what CC-036 adds later.
3. Project `CLAUDE.md` §4: replace "no Android build profile or build script is committed yet" with the build command.
4. Test plan: new area "Android build" (script run, install over an older build, launch and play) and fold in the feature test plan below. Wiki index and log entries.

**Definition of Done**
- [ ] The APK installs (or the reason it didn't is recorded)
- [ ] New page in `index.md`; `CLAUDE.md` §4 updated; test cases follow the test plan format
- [ ] Reviewed by primary; committed per user-level §4

## Tests
- **Unit (EditMode):** build argument parsing and output naming (CC-004.2).
- **System:** the scripted build itself, and the APK's package and version checked with the SDK tools (CC-004.2); install on a device (CC-004.3).
- **MANUAL:** the device smoke test below.

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-004.1 | Medium | Sonnet | Settings and profile asset edits; the primary reviews the YAML diffs |
| CC-004.2 | Medium | Sonnet | Build class, tests and script against a clear spec |
| CC-004.3 | Low | Haiku | Install command and docs; primary review |

## MANUAL (Ronan)
1. **Before CC-004.3: connect a phone.** Enable Developer options and USB debugging on the phone, connect it by USB, and accept the "Allow USB debugging" prompt on the phone. Tell the agent when it's connected.
2. **After the install: device smoke test.** On the phone, launch Cathartic Colours. Start a game in each of the three modes, play a few moves with swipes, check that sounds play, go back to the main menu and resume. Report anything that differs from the editor.

## Decisions
- **D1 — Application id `com.catharticcolours.game`** (Ronan, 2026-11-19). It's permanent once the app is on Google Play.
- **D2 — Version code = commit count of HEAD.** It always grows along `develop` and `master`, needs no stored counter, and is reproducible for a given commit. The version name stays a manual `bundleVersion` bump. CC-036 confirms it works for store uploads.
- **D3 — Development profile builds an APK with no debugger wait** (Ronan, 2026-11-19). An APK installs with `adb`, and the app starts normally; a debugger can still attach later.
- **D4 — Build code in its own Editor assembly.** It follows the `CatharticColours.Simulation` assembly pattern, so it can be unit-tested; code in the default editor assembly can't be referenced by test assemblies.

## Divergences
_None yet._

## Feature test plan
1. `tools/build-android.ps1` on a clean checkout: exit code 0, APK in `Cathartic Colours/Builds/Android/`, package `com.catharticcolours.game`, version code equal to `git rev-list --count HEAD`.
2. `adb install -r` of a newer build over an older one succeeds.
3. On the phone: the app launches without waiting for a debugger; the three modes play with sound; resume works.

## Status log
- 2026-11-19: Planned
- 2026-11-19: Approved by Ronan (plan, D1–D4 and routing; signing split out to CC-036)
