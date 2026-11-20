# CC-043 — Faster Android development builds

| Field | Value |
|---|---|
| Issue | [#76](https://github.com/rtumelty/Cathartic-Colours/issues/76) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-043-android-build-times` (from `develop`, created by the planning session on approval) |
| PR | — |
| Related | [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10) (build pipeline), [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29) (performance), [build-android](../systems/build-android.md) |

## Goal
Find out where the time goes in a development build made with `tools/build-android.ps1`, and cut it. **Target:** an unchanged rebuild in about 2 minutes, and a rebuild after one script change in under 5. If that can't be reached, explain the remaining time step by step. Release settings aren't changed.

## What the existing logs already show (planning, 2026-11-20)
Two recent development builds, both incremental (the Library folder was already warm):

| | 2026-11-19 23:25 (CC-002) | 2026-11-20 00:24 (CC-041) |
|---|---|---|
| Total (`BuildReport.summary.totalTime`) | 21:07 | 17:17 |
| IL2CPP C++ files recompiled | 543 (4,148 s summed over 8 cores) | 378 (3,109 s summed) |
| Gradle (`Building Gradle project`) | 125 s | 126 s |
| IL2CPP code generation / UnityLinker | 22 s / 15 s | 24 s / 11 s |

- **APK size:** 136 MB for about 10 MB of game assets. `libil2cpp.so` is 352 MB uncompressed.
- **Where the time goes:** compiling and linking IL2CPP C++, plus about 2 minutes of Gradle. Assets and shaders are minor.
- **How the two build profiles differ:**
  - **Development profile:** script debugging on, profiler connection on, full debug symbols (level 4).
  - **Release profile (`Android™.asset`):** all three off, with a lower symbol level (2).
  - **Shared:** both use the same player settings (IL2CPP, stripping, code generation).

## Approach
- **Use Unity's own tools first.** Custom tooling is added only where they leave a gap, and the gap is recorded on this page. The official sources:
  - **Bee build profile:** `Library/Bee/fullprofile.json` and the `*.traceevents` files. Bee writes them on every build. They use Chrome's trace format, and Perfetto (`ui.perfetto.dev`, which runs locally in the browser) or `chrome://tracing` opens them. They show every compile, link and copy step on a timeline, per core.
  - **Build report:** `Library/LastBuild.buildreport`, read with Unity's [Build Report Inspector](https://docs.unity3d.com/Packages/com.unity.build-report-inspector@latest) package. It shows step timings and what is included in the build, by size.
  - **Editor log:** the `Android PostProcess task … took` lines (Gradle and the other post-process tasks).
  - **Gradle:** its own `--profile` report (a local HTML file). Use it only if Gradle's 2 minutes need breaking down. Gradle's online build scans publish to an outside service, so they're not used.
- **Release build as the reference.** A release build already has the debug features off. Comparing a development build with a release build shows how much those features cost before anything is changed one at a time.
- **Defender first.** It's the cheapest change and affects every later measurement, so Ronan adds the exclusions before the baseline (MANUAL 1). A before-and-after pair shows its effect.
- **Other software on the machine** (Ronan's editor in the manual copy, other work) is accepted as background, a sunk cost. The timing runs don't wait for it to close, and Ronan can keep working during builds.

## Scope
**In:**
- the development build profile (`Assets/Settings/Build Profiles/Android™ Development.asset`);
- Android player settings that only affect build speed or debug features;
- the build script, editor build code and development Gradle properties;
- the Build Report Inspector package, if it's used (editor-only);
- the timings on the wiki page.

**Out:**
- Changes to the release profile ([CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57)). Release builds are only *run* here, for comparison.
- Runtime optimisation ([CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29)).
- Removing packages the game uses.

## Deliverables

### CC-043.1 — Defender exclusions, measurement setup and baselines
**Sub-issue:** [#81](https://github.com/rtumelty/Cathartic-Colours/issues/81) · **Risk:** Low · **Worker:** Sonnet · **Review:** primary
1. **Before:** one unchanged development rebuild with Defender as it is now.
2. **MANUAL 1:** Ronan adds the exclusions. Then repeat the same rebuild. Record both.
3. **Measurement setup.**
   - Add the Build Report Inspector package, an editor-only dev dependency.
   - Check what Unity's sources show for one build: the Bee profile in Perfetto, the build report, and the log's post-process lines.
   - Record on this page how to read them, and any gap.
   - Add custom tooling only for a gap that matters. The likely one is that batch mode leaves no single summary in the log. In that case `AndroidBuild` logs a short table from `BuildReport.steps` after each build, with EditMode tests. If there's no gap, nothing is written.
4. **Release reference.**
   - `build-android.ps1` gains `-Profile <name>`. The default stays the development profile.
   - The editor method builds the named profile and keeps its version-code handling. Covered by EditMode tests in `AndroidBuildArgs`.
   - Release builds with no keystore set use Unity's debug signing, which is fine for a local timing build. Nothing is published.
5. **Baselines, with the exclusions in place:**

   | Profile | Clean (`Library/Bee` and `Library/Il2cppBuildCache` deleted; both are caches) | Unchanged rebuild | One-line script change |
   |---|---|---|---|
   | Development | | | |
   | Release | | | |

   For each run, record the total time, the top steps from the Bee profile, Gradle's time, the APK size and the `libil2cpp.so` size.

**Tests:** EditMode tests for `-Profile` handling, and for any custom summary, if one is added.

**Definition of Done** (template plus):
- [ ] Defender before-and-after pair, and the baseline table, on this page
- [ ] How to read the official tools written on the [build-android](../systems/build-android.md) page; gaps listed, each with what was done about it
- [ ] EditMode suite passes from the command line

### CC-043.2 — Close the gap, one setting at a time
**Sub-issue:** [#82](https://github.com/rtumelty/Cathartic-Colours/issues/82) · **Risk:** Medium · **Worker:** Sonnet runs the builds; **primary** reads the results and picks the changes · **Review:** primary

The order depends on what the baselines show. If release is much faster than development, start with the settings the two profiles differ in:
1. Script debugging off.
2. Profiler connection off.
3. Debug symbol level 4 → 2, or none.

If both are slow, the shared player settings come next:

4. IL2CPP code generation for faster builds.
5. Managed stripping Low, then Medium. Check each with the PlayMode smoke tests and a device run, since stripping can remove code that's only reached by reflection (Entities, FMOD, IAP).
6. Keep test assemblies (`Unity.Collections.Tests.*`) out of the player build, if a setting causes it.
7. Gradle properties for development builds (daemon, parallel, build cache).
8. Mono backend for development builds only, if Unity 6.6 supports it on ARM64.

For each change, run an unchanged rebuild and a one-line-change rebuild, and record the time, the APK size and the main Bee steps. Revert the change before the next one, unless it's clearly safe and becomes the new base (recorded on the page). Stop once the target is met.

**Tests:** none new. EditMode and PlayMode pass for every kept setting, and the APK installs and starts on a device, if one is attached.

**Definition of Done:**
- [ ] Results table, one row per experiment, kept or reverted
- [ ] A recommendation per setting with its cost (for example, script debugging off means no managed debugger on device)
- [ ] Anything that changes what a development build can do goes to Ronan before CC-043.3

### CC-043.3 — Apply the chosen settings and document them
**Sub-issue:** [#83](https://github.com/rtumelty/Cathartic-Colours/issues/83) · **Risk:** Medium · **Worker:** Sonnet · **Review:** primary
1. Apply the settings Ronan chose to the development profile. If on-device debugging is still wanted, keep a second profile, `Android™ Development (Debuggable)`, built with `-Profile`.
2. Update [build-android](../systems/build-android.md): timings before and after, what each setting does, how to get a debuggable build, the Defender exclusions and how to read a slow build with the official tools. Also update the TP-DROID test-plan case with the expected time.
3. Final runs: a clean build and an unchanged rebuild. Then install the APK and play a game.

**Definition of Done:**
- [ ] Unchanged rebuild about 2 minutes or less, or every remaining minute explained
- [ ] Release profile unchanged (diff checked)
- [ ] build-android page, test plan, index and log updated

## Risk and model routing
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-043.1 | Low | Sonnet | Official tools first; small script change; baseline runs |
| CC-043.2 | Medium | Sonnet runs, primary judges | Stripping can break reflection, so smoke tests and a device run are needed |
| CC-043.3 | Medium | Sonnet | Settings and docs |

Until the experiments pay off, a build takes about 15 to 20 minutes. CC-043.1 needs about 8 builds and CC-043.2 up to about 16. They run in the background, and commits don't wait for them (user-level §2.2).

## MANUAL (Ronan)
1. **Start of CC-043.1, after the "before" run: Windows Defender exclusions.** The session asks when it's ready.
   - **Where:** Windows Security → Virus & threat protection → Manage settings → Exclusions → Add or remove exclusions → Add an exclusion → Folder.
   - **Add:**
     - `E:\Projects\Cathartic-Colours\Cathartic Colours\Library`
     - `E:\Projects\Cathartic-Colours\Cathartic Colours\Temp`
     - `%USERPROFILE%\.gradle`
     - `C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Data\PlaybackEngines\AndroidPlayer` (the SDK, NDK and JDK)
   - Optionally add the same `Library` and `Temp` folders for the manual copy.
   - Exclusions lower protection for those folders, so it's your call. Remove any that don't help.

## Decisions
- **D1 — Development builds only.** Release settings stay as they are until CC-036. Release builds are run here only as a reference.
- **D2 — Official tools first** (Ronan, 2026-11-20): the Bee profile, build report and log. Custom tooling only fills a recorded gap.
- **D3 — Defender exclusions first** (Ronan, 2026-11-20), measured before and after.
- **D4 — Background software is a sunk cost** (Ronan, 2026-11-20). Timing runs don't need other apps closed.

## Divergences
_None yet._

## Feature test plan
1. `tools/build-android.ps1` twice with no changes: the second run meets the target. Open `Library/Bee/fullprofile.json` in Perfetto to see the steps.
2. `tools/build-android.ps1 -Profile "Android™"` builds a release-settings APK.
3. Install the development APK, start a game in each mode, play a few moves with sound.

## Status log
- 2026-11-20: Planned; revised after Ronan's review (official tools, release baseline, Defender first, background software accepted)
- 2026-11-20: Approved by Ronan (three deliverables, routing as above)
