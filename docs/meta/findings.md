# Findings — strengths and weaknesses of each approach

Draft, first pass 2026-11-21. Evidence is cited by commit, PR, issue or file. See [timeline.md](timeline.md) for dates and [`data/`](data/) for chart data.

## What changed since last time
- **2026-11-21 (second pass):** Ronan's answers folded in: the talk is a 30-minute slide talk for engineers and tech artists; the comparison with Unity's AI Assistant should be candid; the Assistant wrote most of Era 2 after Ronan's hand-written barebones ECS pass; the 9-month gap was distraction and motivation. Claude Code transcripts read (with permission): new section "What the workflow cost Ronan", CC-008 timing confirmed, era boundary refined. Era 2 chat transcripts may be available from another machine on Monday 2026-11-23.
- **2026-11-21 (first pass):** timeline built from git, PRs, issues, wiki log, feature pages, ADRs and memory. Covers everything to `4d16f8a` (CC-046/047 planned; CC-046 in progress). Transcripts not read yet.

---

## Headline numbers

| | Era 1 PuzzleScript | Era 2 Unity + AI Assistant | Era 3 Claude Code (4 days) |
|---|---|---|---|
| Active days | 2 | ≈14 over 6 weeks | 4 |
| Commits | 2 | 45 | 134 (130 co-authored by Claude) |
| Game code at end | 252-line .pz | 3,289 lines C# (44 files) | 4,205 lines C# (54 files) |
| Test code / tests | — | 0 / 0 | 6,694 lines / 442 EditMode + 2 PlayMode |
| Docs | none | none (no README, no plans) | 4,587-line wiki (35 pages): 10 system pages, 12 feature pages, 5 ADRs, 83 test cases, 171 log entries |
| Tracking | — | none | 106 GitHub issues on a project board |
| Branching / review | — | everything on `master`, one abandoned side branch | `develop` + 13 feature branches, 12 squash-merged PRs, Ronan merges |
| Player-visible features added | the whole design | the whole Unity game (3 modes, menu, audio, particles, shaders, save) | none new; 25 bugs/chores fixed, rotating saves, Android builds |

That last row is the honest centre of the talk: **Era 2 built the game; Era 3 has so far built the engineering around it.**

---

## Era 1 — PuzzleScript prototype (April 2021)

**Fastest at:** proving the design. A complete, playable rule set (three colours, three sizes, merging, a "next colour" indicator, sound) in one 252-line declarative file, then "colour foreshadowing" two weeks later.

**Strengths**
- The design survived intact. Every later era implements the same rules; the wiki still treats the .pz as the reference source (`docs/raw/README.md`).
- Zero engineering overhead. PuzzleScript's rewrite-rule language *is* the spec.

**Weaknesses**
- A dead end technically: no path to Android, persistence, modes or monetisation. Nothing was carried forward but the design.
- One prototype feature, the next-spawn preview, has still not reached the Unity game (CC-007, #19, open). Five years on, the prototype is ahead of the product on that point.

---

## Era 2 — Unity with the AI Assistant (Jan–Feb 2026)

**Fastest at:** going from nothing to a feature-complete vertical slice. 937 lines of ECS game on day 1. On **2026-01-15 alone** (13 commits): two new game modes, a strategy refactor for merge rules, per-mode profiles and instructions, a main menu. Within ~14 active days: FMOD audio, particles, jelly shaders, a 3D renderer switch and a save system.

**Strengths**
- **Speed to a playable game.** Everything a player sees today comes from this era.
- **Reasonable instincts in the architecture.** DOTS/ECS from the start, ScriptableObject configuration (`0c7e384`, `bf5152d`), merge rules as strategies (`3fc0cdc`), event entities for audio. Era 3's systems pages describe these as the patterns to follow (project `CLAUDE.md` §3).
- **Small, honest commits.** Messages like "Fix issue caused by too frequent input" or "Cache SpriteRenderers … to minimise GetComponent calls" read fine.

**Weaknesses (what Era 3 had to fix)**
- **No tests, and no way to add them.** All game code compiled into `Assembly-CSharp`, which test assemblies can't reference (old `CLAUDE.md` §4, "Known constraint"). Fixed by CC-006 (Simulation assembly) and CC-002 (Core/Presentation, ADR-004).
- **Rules smeared across the frame.** One move was split over five systems coordinating through tags and partly unconstrained ordering; simulation code called presentation directly (`InputProcessingSystem → JellyManager.StartWobble`, `ScoringSystem → PointsParticleSpawner`). ADR-001: *"bugs CC-020–CC-024 live in the seams between systems."* The `IMergeUtility` classes also blocked Burst.
- **Latent bugs.** Reading the code for the wiki (CC-001, 2026-11-18) produced 13 backlog items in a few hours (CC-020–CC-032), and later work found 4 more (CC-037, CC-038, CC-044, CC-045). Fixed so far:

  | Fixed in | Items | Examples |
  |---|---|---|
  | CC-006 (PR #56) | CC-020–CC-024 | colour roll didn't advance the RNG; spawn could fail with empty cells; **resume replayed the resolve/spawn step**; move sound on rejected moves; dead ECS code |
  | CC-041 (PR #80) | CC-025, 027–032, 037, 038, 044 | restarts stacked duplicate grids; double-firing restart buttons; menu settings leaking into resumed games; non-atomic save writes; up-arrow threw an exception on every press; a log line on every move; hard-coded keyboard instead of the project's input actions; resumed games re-seeded the RNG |
  | still open | CC-019, CC-026, CC-045 | unreachable level-complete panel, mode rule gaps, broken URP references |

- **Project hygiene left traps.** The FMOD Studio project was never saved: the game's five events existed only in `.unsaved/` (old `CLAUDE.md` §5). The manifest had 60 packages, including the pre-release `com.unity.ai.*` set that CC-033 removed first because it could block the 6.6 package resolution (CC-033 page, D1).
- **No record of intent.** No plans, decisions or docs survive from this era, so Era 3's first day was archaeology (CC-001: eight system pages reverse-engineered from code).

**Authorship (Ronan, 2026-11-21):** Ronan wrote the barebones ECS implementation by hand (`a898b66`, 937 lines, committed three hours before the Assistant package), then the Assistant wrote "most" of the rest. So roughly 2,350 of the era's 3,289 final lines are Assistant-led, including the whole 2026-01-15 burst. There are no co-author trailers, so this rests on Ronan's account, not git.

**Candid read on the Assistant (for the talk):**
- It was very good at the thing it is built for: changing a project you have open in the editor, quickly. Three modes, a menu and audio in a few days is a real result.
- Nothing in the way it was used pushed towards tests, assembly boundaries, docs or reviewable units of work, and the evidence shows none appeared. The bugs found later (resume replaying a step, RNG not advancing, restarts stacking grids) are the kind that tests and a separated simulation catch.
- Its packages became a liability. `com.unity.ai.assistant 1.0.0-pre.12`, `ai.generators` and `ai.inference` were pre-release, unused at runtime, and the first thing stripped so the 6.6 upgrade could resolve (CC-033 D1). `ai.inference` also pulled in `com.unity.dt.app-ui`, left behind in `EditorBuildSettings`.
- Fairness caveat: the difference is as much *process* as *tool*. Ronan worked with the Assistant the way most people do (prompt, accept, commit) and with Claude Code inside a heavy, explicit process. An Assistant run with tests and plans would be a different comparison.

**Why the 9-month gap (Ronan):** "Distraction, motivation." Worth one line in the talk: the prototype-to-Unity jump stalled for the same reason most side projects do, and the restart came with a new workflow, not new code.

---

## Era 3 — Claude Code workflow (18 Nov 2026 →)

**Fastest at:** foundations and refactors with a safety net. In four days: Unity 6.0 → 6.6 upgrade (CC-033, 567 files), a pure Burst simulation with 200 tests (CC-006, implementation ≈4.7 h), a command-line Android pipeline (CC-004), assemblies and PlayMode tests (CC-002), eleven fixes in 94 minutes (CC-041), build times cut 60% (CC-043), and a behaviour-preserving rules refactor proved by 300 recorded games (CC-008, PR 16 min after the plan commit).

**Strengths**
- **Tests became the substrate.** 0 → 4 → 200 → 288 → 367 → 442. CC-008 is the best example: record 300 seeded games (3 modes × spawn on/off × 50 seeds, hashed per move) *before* refactoring, then prove the refactor changed nothing (`recorded-games.txt` unchanged after `1abbc7b`). That is the move that makes agent refactors safe.
- **Architecture improved in measurable steps, each with an ADR.** ADR-001 (pure simulation, Burst), ADR-004 (assemblies), ADR-005 (composable rule families). Five move systems became one Burst `BoardStepSystem`.
- **Evidence-led engineering on Unity problems.** CC-043 used Unity's own tools first (Bee trace events, the build report, editor log), took baselines, then changed one setting at a time: one-line dev rebuild 4:33 → 1:49, clean 31:14 → 13:17, `libil2cpp.so` 352 → 96 MB. It records what didn't work (Build Report Inspector 0.3.0-preview doesn't compile on 6.6; profiler off gave nothing; Engine Diagnostics off saved 9 s but recompiled all C++, reverted).
- **Knowledge is written down.** Every decision, divergence and approval is in the log; each PR body stands on its own (memory `readable-commits`).
- **Cycle time collapsed as trust grew.** Plan-to-PR: 3.2 h (CC-001, approval stop per deliverable) → 0.6 h (CC-041, no stops) → 9–16 min (CC-039, CC-042, CC-008). Two features ran in parallel on day 3.
- **Cheaper models carried real work.** 30 deliverables routed to Haiku (11) or Sonnet (18+) vs 7 kept on the primary model; feature pages report no worker output rejected twice (e.g. CC-041: "none was rejected").

**Weaknesses**
- **No new gameplay yet.** Game code grew 28% (3,289 → 4,205 lines) while tests and tooling grew from 0 to 6,694 lines and the wiki to 4,587. Defensible as foundations, but the talk should say it plainly.
- **Process overhead is real.** 171 log entries and 21 workflow-rule changes in 4 days; 10 memory files of corrections. 74 of 132 non-merge Era 3 commits (56%) touch only Markdown/docs (6,924 changed `.md` lines on day 1 alone vs 3,276 `.cs`). Day 1 was mostly building the process.
- **It made its own mistakes.**
  - A commit pushed after a squash merge was silently lost (`940c674`, recovered as `8a8eed7`).
  - CC-004's version-code design (commit count per branch) went backwards between branches within a day (CC-042 APK code 82 < CC-041's 88); fixed by CC-039.
  - Agents proposed manual steps they could do themselves, worked around a missing `gh`, messaged another session unasked, and wrote a first quiz that tested recall. Each became a rule.
  - Unity batchmode rewrote ProjectSettings with LF endings on every run (reverted each time; CC-040 for the real re-saves); a first Burst test run in a fresh worktree failed as "ran as managed code".
- **It can't do everything a Unity dev does.** Hub CLI headless install was gone and elevation refused, so the Android module came via the Hub GUI (CC-033); Defender exclusions, FMOD Studio saves, device smoke tests and merges stayed with Ronan. The two-working-copies rule exists because batchmode can't run while Ronan has the editor open.
- **Device builds are the bottleneck.** First Android build 36 min, typical incremental 17–21 min before CC-043; the rules "don't hold a commit for a long build" and "device builds only at PR time" exist because of this.
- **Attention cost.** Commits run from 07:37 to 03:41 (CC-041 merged at 02:10). Ronan approved every deliverable early on and answers a quiz per feature (CC-039 quiz: 1 of 4 right, 1 partly). The workflow moved toward fewer stops and Remote Control so he could step away — that trend is the story, but the hours were long. Details in "What the workflow cost Ronan" below.

---

## What the workflow cost Ronan (from the Claude Code transcripts)

Source: 20 session transcripts in `~/.claude/projects/E--Projects-Cathartic-Colours*` (read 2026-11-21 with permission; deduplicated by message id, forks counted once). Ronan's words below are his own messages.

| | 18 Nov | 19 Nov | 20 Nov | 21 Nov (to 11:11) |
|---|---|---|---|---|
| Ronan's typed messages | 54 | 62 | 21 | 9 |
| First / last message | 11:14 / 21:14 | 10:02 / 23:59 | 00:04 / 23:23 | 07:28 / 11:11 |

- **146 messages in 4 days; 59 (40%) are 30 characters or fewer**, mostly "Approve, start .2". The workflow turns Ronan into an approver more than an author.
- **Attention fell as trust rose.** 54 and 62 messages on days 1–2, then 21 and 9 once plans ran with "no approval stops". On 20 Nov Ronan asked for tasks that "require little manual intervention" because he was away for two days; CC-039 and CC-042 then ran in parallel overnight.
- **CC-008 is the extreme:** "Make a plan for 008" 09:46 → "I approve" 09:52 → PR 10:10 → merged 10:47. Two messages from Ronan plus the quiz answer. The transcript confirms the speed is real: batchmode test runs of about 30 s, a Sonnet worker for CC-008.1 and .3, a Haiku worker for the test plan, the primary model writing the rule families itself.
- **The cost moved to understanding.** Quiz answers contain "I don't know" 5 times, e.g. CC-003 (three of four answers) and CC-008: "I don't know why you opted for switches over structs". The quiz was added to keep Ronan close to agent-written code; it is also the clearest measure of how far he drifted.
- **Ronan's corrections set the rules.** Examples:
  - process: "The commit messages and pull requests are too arcane" (18 Nov) → readable-commit rule;
  - authority: "I did not ask you to inform the other session … You should not have done that without express confirmation" (19 Nov) → never message sessions unasked;
  - engineering taste: "there is existing tooling from unity for analyzing build times. We should leverage the official tools first" (20 Nov) → CC-043's whole approach;
  - quiz quality: "I don't want a memory test of class names" (18 Nov).
- **Friction with the tools themselves:** cloud sessions couldn't read the user-level `CLAUDE.md` ("This feels like an annoying constraint", 18 Nov), so everything runs locally with Remote Control; session cards don't show in the mobile app, so the planning session had to start sessions directly; 10 context compactions across sessions; waiting on builds ("This has been stalled a long time. What is happening", CC-043).
- **Ronan still did the Unity-only work:** Hub GUI install of the Android module, Defender exclusions, FMOD Studio saves and bank rebuilds, listening checks ("All sounds OK"), device smoke tests, editor-log reports, and every merge.

## Cross-era comparison

| Question | Era 2 (AI Assistant) | Era 3 (Claude Code) |
|---|---|---|
| Where the AI lives | Inside the editor | Terminal/desktop app, drives the editor in batchmode |
| Unit of work | A prompt → code in the scene | A planned deliverable → reviewed commit → PR |
| What it sees | The open project | The repo, git, `gh`, the wiki, logs, build output; not the running editor |
| Tests | none | 442, run from the CLI every deliverable |
| Docs | none | a wiki it maintains itself |
| Good for | Getting a game on screen | Changing a game safely |
| Weak at | Structure that survives the next change | New visual/gameplay work, editor-only tasks |

Caveat: this compares two very different *ways of working* as much as two tools. Era 2 had no process; Era 3 has a heavy one. The talk should not imply the Assistant could not have been used with tests and plans.

## Unity-specific notes for the talk
- **DOTS/ECS + Burst:** a pure, blittable simulation (`BoardSimulation.Step<TRules>`, struct rules behind `IMergeRules`) that ECS wraps, so rules are unit-testable without a World and Burst-compiled in it (ADR-001, then ADR-005's rule families).
- **Unity 6.6 upgrade:** strip pre-release packages *before* the first open; upgrade in batchmode; 57 `.meta` files below 6.6's minimum format re-saved.
- **Batchmode testing:** `-runTests` without `-quit`; results land relative to the project folder; Burst cache first-run effect.
- **Android builds:** IL2CPP C++ compile + ~2 min Gradle dominate; symbol level and script debugging are the levers; build profiles per purpose (Development, Debuggable, Release).
- **FMOD:** save the Studio project, rebuild banks from the CLI (`fmodstudiocl`), check which bank folder Android actually packs (it was Desktop).
- **Automated playthroughs** (CC-046/047, in progress): the next step for letting agents verify gameplay, in the editor and on a device.

## Open evidence gaps
- Era 2 Assistant chat transcripts (Ronan may have them on another machine, Monday 2026-11-23): which features were used, how prompts looked, what was accepted or rewritten.
- Ronan's actual hours per era (he isn't sure; message and commit times are only a proxy).
- Unity AI Assistant as of today vs the January pre-release: the talk should say which version was used (1.0.0-pre.12).
