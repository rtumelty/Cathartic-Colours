# CC-043 — Faster Android development builds

| Field | Value |
|---|---|
| Issue | [#76](https://github.com/rtumelty/Cathartic-Colours/issues/76) (status and fields live on the [board](https://github.com/users/rtumelty/projects/1)) |
| Branch | `feature/CC-043-android-build-times` (from `develop`, created by the planning session on approval) |
| PR | [#84](https://github.com/rtumelty/Cathartic-Colours/pull/84) |
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

## Results

### CC-043.1 — measurements (2026-11-20)
All runs used `tools/build-android.ps1`, with Ronan's editor open in the manual copy as background (D4). "Total" is the script's elapsed time, and Unity's own `totalTime` is about 30 s less (editor start-up and shutdown). Logs and traces are in the session scratchpad, not the repo.

**Defender exclusions (MANUAL 1).** The before-and-after pair wasn't like for like. The "before" run also had a version-code change: the last APK came from the CC-041 branch, code 88, and this branch counts 78. A third run, with exclusions and only the version code changed, separates the two effects:

| Run | Total | Gradle | Asset refresh at start | Symbol upload step |
|---|---|---|---|---|
| Before exclusions (version code 88 → 78) | 3:54 | 129 s | 21 s | 21 s |
| After exclusions, nothing changed | 1:05 | 12 s | 7 s | 10 s |
| After exclusions, version code 78 → 79 only | 3:02 | 120 s | 8 s | 9 s |

The exclusions save about 25 s (asset refresh and the symbol step), and they're kept. The Gradle difference comes from the version code.

**Baselines** (exclusions in place, version code fixed at 79):

| Profile | Clean (`Library/Bee` deleted) | Unchanged rebuild | One-line script change |
|---|---|---|---|
| Development | 31:14 | 1:05 | 4:33 |
| Release (`-Profile Release`, App Bundle) | 13:52 | 1:19 | 1:57 |

| Output | Package | `libil2cpp.so` inside |
|---|---|---|
| Development APK | 136.5 MB | 352 MB uncompressed (99 MB compressed) |
| Release AAB | 79.2 MB | 91 MB, with its 78 MB of symbols kept apart in bundle metadata |

**Development one-line change, step by step** (4:33; from `Library/Bee/buildreport.json`):

| Step | Time |
|---|---|
| Asset refresh at start-up, script compile, prepare | about 25 s |
| UnityLinker (managed stripping) | 10 s |
| IL2CPP code generation | 20 s |
| C++ compile (3 object files) and Burst | 11 s |
| Link `libil2cpp.so` | 24 s |
| Copy the 352 MB `libil2cpp.so` to the symbols folder and the Gradle project | 24 s |
| `objcopy` strip | 4 s |
| Unity Services Symbol Upload (scans the symbols, then stops: no Unity Cloud token, so nothing is uploaded) | 9 s |
| Gradle | 120 s |
| Moving the APK | 6 s |

**What this shows:**
1. **The 17–21 minute builds from planning weren't unchanged rebuilds.** They followed C# changes on other branches, and Bee had hundreds of object files missing (`OutputFilesMissingFor`, 259 in the 2026-11-20 00:24 build). An unchanged rebuild doesn't recompile C++.
2. **Switching profiles throws away the other profile's C++ cache.** The development rebuild after the release runs took 30:01, the same as a clean build. Each switch costs one clean build, which matters if CC-043.3 keeps a second, debuggable development profile.
3. **Gradle is the biggest cost for any rebuild that changes something.** That includes every new commit, because the version code is the commit count. It takes 120 s in development, but release's whole one-line rebuild is 117 s. The likely cause is the 352 MB unstripped `libil2cpp.so` (symbol level 4) that Gradle compresses into the APK.
4. **Debug features roughly double the clean build:** 31 minutes for development against 14 for release.

### Gaps in the official tools
- **Build Report Inspector:** `com.unity.build-report-inspector` 0.3.0-preview, the latest, from 2022, doesn't compile on Unity 6.6 (`BinaryFormatter`, analyzer error UAC0023), so it was reverted. Gap: no size-by-asset view. Nothing was done about it, since the asset content is about 10 MB and isn't the cost.
- **Per-run summary in batch mode:** not needed. The `PostProcess task … took` lines in the log plus `Library/Bee/buildreport.json` give it, and the [build-android](../systems/build-android.md#reading-a-slow-build) page shows how to read them. No custom tooling was added.

### Input for CC-043.2
Suggested order, by the evidence above:
1. Debug symbol level 4 → lower. This targets Gradle's 120 s and the 24 s copy.
2. Symbol upload off for development builds (9 s).
3. Script debugging and profiler connection, which drive the 31 against 14 minute clean build.
4. A fixed version code for development builds, so a new commit doesn't make Gradle repackage. Android allows installing an equal version code over itself.

### CC-043.2 — experiments (2026-11-20)
Development profile, version code fixed at 79, Defender exclusions on. The experiments were cumulative: E1 stays in, E2 was timed and then reverted, and E3a and E3b were added on top of E1. "Objects" is the number of C++ object files recompiled. Runs by the Sonnet worker, read and judged by the primary model.

| Exp | Change | First build after the change | Unchanged | One-line change | `libil2cpp.so` | Result |
|---|---|---|---|---|---|---|
| — | Baseline (CC-043.1) | — | 1:05 | 4:33 (Gradle 120 s) | 352 MB | — |
| E1 | Debug symbols Full (4) → SymbolTable (2) | 1:15, 0 objects | 1:02 | **2:46** (Gradle 28 s) | 352 MB | **Meets the target.** New base |
| E2 | Engine Diagnostics off (`UnityConnectSettings.asset`, project-wide) | 29:40, 874 objects | — | — | 352 MB | Symbol step 9 s → 0 s, but toggling it recompiles all C++. Reverted |
| E3a | E1 + script debugging off | 13:47, 869 objects | 1:11 | **1:49** (Gradle 16 s) | **96 MB** | Fastest. No managed debugger. Awaiting Ronan |
| E3b | E3a + profiler connection off | 1:47, 2 objects | 0:54 | 1:42 | 96 MB | No real gain (within noise). Not recommended |

**Notes**
- **Why E1 helps:** with level Full, Unity's Gradle project sets `debugSymbolLevel "full"`, and Gradle extracts full native debug metadata from the 352 MB library on every changed build. SymbolTable keeps function names in native stack traces and loses only file and line numbers. Managed (C#) debugging is unaffected.
- **E1 side effect:** after a one-line change, the APK file on disk was 235 MB for 136 MB of contents. Gradle's incremental packaging leaves the old compressed `libil2cpp.so` (99 MB) as dead space. It installs the same, but `adb install` copies the larger file. The dead space didn't come back after the E3a rebuild.
- **Why E3a helps:** script debugging makes IL2CPP emit debugger sequence points and checks in every method. That makes the C++ 3.7× larger (352 → 96 MB) and doubles a clean build (31 against 14 minutes, the same as release).
- **Not run:**
  - The shared player settings (IL2CPP code generation, stripping, test assemblies), Gradle properties and Mono. The target was met first, and the plan says to stop there.
  - E4, a fixed version code. With E1, a version-code-only change costs about the same as a one-line change, because the Gradle step is the part that changed, so it isn't needed.
- **Tests:** no code changed in CC-043.2. No phone was attached, so no device install was done; that happens in CC-043.3.

**Recommendations for CC-043.3** (each with its cost):
1. **Keep E1 (SymbolTable).** It cuts a one-line rebuild from 4:33 to 2:46. Cost: native crash stacks show function names but no line numbers. Full symbols are still possible with a debuggable profile.
2. **Script debugging (E3a): Ronan's call.**
   - Off: a one-line rebuild drops by another minute (2:46 → 1:49) and the APK's library by 73%.
   - Cost: no attaching Rider or Visual Studio to the game on the phone.
   - If debugging is still wanted now and then, keep a second profile, `Android™ Development (Debuggable)`, built with `-Profile`. Switching between the two profiles recompiles all C++, about 14 to 30 minutes each way.
3. **Keep the profiler connection on (E3b off).** It costs nothing measurable and is needed for on-device profiling ([CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29)).
4. **Leave Engine Diagnostics on for now (E2).** It saves 9 s per build, but it's project-wide: it also changes release builds and what the shipped game reports to Unity. Toggling it recompiles all C++. It's a privacy and release decision, so it fits [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57) better.

### CC-043.3 — final runs (2026-11-20)
The settings Ronan chose: debug symbols Symbol Table, script debugging off, profiler on, plus a `Debuggable` profile with script debugging on. Default version code (commit count).

| Run | Total | Gradle | Objects | APK | `libil2cpp.so` |
|---|---|---|---|---|---|
| Development, clean | **13:17** (was 31:14) | 28 s | 869 | 64 MB | 96 MB |
| Development, unchanged | **1:11** (target about 2 min) | 10 s | 0 | 64 MB | 96 MB |
| Debuggable (profile switch) | 28:15 | 26 s | 1,000 | 163 MB | 352 MB |
| Development after Debuggable (switch back) | 12:19 | 18 s | 869 | 91 MB (dead space, see CC-043.2) | 96 MB |

The one-line-change time for these settings is the E3a figure from CC-043.2, **1:49** (target under 5). The Debuggable run's 352 MB library matches script debugging being on. Its wrapper exited 1 after Unity exited 0, because the script's last `Write-Host` failed with output redirected; the APK was written ([build-android](../systems/build-android.md#running-a-build) notes it). Release profile: no diff against `develop`. EditMode (330) and PlayMode (2) pass. No phone was attached, so the device check is TP-DROID-04 for Ronan.

**Where the remaining minute goes in an unchanged rebuild:** editor start-up and the asset refresh (about 15 s), Bee checking about 2,500 nodes (about 10 s), the Android SDK check (3–6 s), the symbol upload step (7 s, see E2), Gradle (10 s), moving the APK (2 s), and editor shutdown.

## Divergences
- **CC-043.1, `-Profile` values:**
  - `-Profile` takes the aliases `Development` (default) and `Release`, or any profile asset name, instead of the asset name only. This keeps the ™ character out of the PowerShell 5.1 command line.
  - The parameter's variable is `$BuildProfile` with the alias `-Profile`, because `$Profile` would hide PowerShell's `$PROFILE`.
  - The output is named `...-release.aab` for the release profile, because that profile builds an App Bundle. The release profile is unchanged.
- **CC-043.1, cache folder:** `Library/Il2cppBuildCache` doesn't exist in Unity 6.6, so a clean build deletes `Library/Bee` only.
- **CC-043.1, release runs:** the release unchanged and one-line runs have totals only. A logging mistake in the chained run lost their per-step logs, and rerunning them would mean another clean release build plus a 30-minute development re-warm. Their totals already show the point (Gradle is much shorter for release).

## Feature test plan
Shortest path, in this order (folded into [test-plan.md](../test-plan.md#android-build-droid) as TP-DROID-05 and TP-DROID-06; TP-DROID-04 covers playing on the phone):
1. **TP-DROID-04:** install the development APK (`adb install -r`), start a game in each mode and play a few moves with sound. This checks the new settings on a device.
2. **TP-DROID-05:** run `tools\build-android.ps1` twice with no changes, then once after a one-line C# change. The rebuilds take about 1 and 2 minutes. Open `Library/Bee/buildreport.json` in Perfetto to see the steps.
3. **TP-DROID-06:** `-Profile Debuggable` builds an APK a debugger can attach to, and `-Profile Release` builds `...-release.aab`. Build the default profile again afterwards. Each switch is a full C++ rebuild.

## Status log
- 2026-11-20: Planned; revised after Ronan's review (official tools, release baseline, Defender first, background software accepted)
- 2026-11-20: Approved by Ronan (three deliverables, routing as above)
