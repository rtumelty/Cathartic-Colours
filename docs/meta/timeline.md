# Cathartic Colours — how it was built (timeline)

Draft, first pass 2026-11-21. Sources: `git log --all` (188 commits), 12 PRs, 106 issues, `docs/wiki/log.md` (171 entries), feature pages, ADRs, auto-memory. Chart data: [`data/`](data/).

## The three eras at a glance

| Era | Dates | Calendar span | Commits | Who wrote the code | Evidence for the boundary |
|---|---|---|---|---|---|
| 1. PuzzleScript prototype | 2021-04-12 → 2021-04-27 | 2 weeks | 2 | Ronan, by hand | `414fc95 Cathartic Colors v1.0`, `f6a480c Add colour foreshadowing` (252-line `CatharticColours.pz`) |
| *Gap* | 2021-04 → 2026-01 | ~4 y 8 m | 0 | | |
| 2. Unity + AI Assistant | 2026-01-08 → 2026-02-16 | 6 weeks (≈14 active days) | 45 | Ronan's hand-written barebones ECS pass, then mostly Unity AI Assistant (`com.unity.ai.assistant 1.0.0-pre.12`, plus `ai.generators`, `ai.inference`) | `087549c` Unity .gitignore 2026-01-08 11:39; package added `d1dc4d0` 2026-01-08 14:57. No co-author trailers, no PRs, all on `master` |
| *Gap* | 2026-02-16 → 2026-11-18 | 9 months | 0 | "Distraction, motivation" (Ronan) | |
| 3. Claude Code workflow | 2026-11-18 → ongoing | 4 days so far | 134 | Claude Code (Opus primary, Sonnet/Haiku sub-agents), reviewed and merged by Ronan | `ee12205 Add CLAUDE.md` 2026-11-18 10:32 (Ronan, no trailer). First `Co-Authored-By: Claude Opus 5.5` commit: `2fdba09` 11:50. 130 of 134 commits carry the trailer; the rest are Ronan's own (CLAUDE.md, wiki setup, FMOD saves) and GitHub merge commits |

**The Claude boundary is 2026-11-18 10:32, not 11-17.** Nothing in git, the wiki, memory or the transcripts is dated 11-17. Ronan committed `CLAUDE.md` at 10:32; the first Claude Code session message ("Read the user and folder CLAUDE.md files") is at 11:14, and the planning session was named at 11:19.

**Era 2 authorship (Ronan).** Ronan wrote the barebones ECS pass by hand (`a898b66`, 17 scripts, 937 lines, three hours before the Assistant package commit). The Assistant wrote most of what followed.

## Era 1 — PuzzleScript (April 2021)

- **2021-04-12** `414fc95` v1.0: 3 colours × 3 sizes (Small/Medium/Large Red, Green, Blue), a `NextColour` object, sounds, collision layers — the whole core rule set in one 252-line declarative file.
- **2021-04-27** `f6a480c` "colour foreshadowing" — the next-spawn preview. (Note: the Unity game still lacks it; it is open as CC-007, #19.)
- **2026-01-08** `c540115` one SFX tweak to the .pz, made on the day Era 2 began. The prototype was still the design reference.

## Era 2 — Unity with AI Assistant (Jan–Feb 2026)

Game code (`Assets/Scripts/**/*.cs`): **937 lines (day 1) → 1,739 (day 8) → 3,289 (end)**. Tests: **0** throughout.

**Burst 1 — the core game, 2026-01-08 → 01-18 (≈9 active days, 41 commits)**
- 01-08 ECS first pass (components, movement, merge, spawn, `GameRenderer`), movement+merge combined, end-game system split out, AI Assistant package added.
- 01-09 FMOD for Unity integrated, FMOD Studio project, audio events for move/merge/game over; input-flood fix.
- 01-12 FMOD event paths moved to properties ("cannot be declared AOT").
- 01-14 animations, `GetComponent` caching, redundant win conditions removed.
- **01-15, 13 commits in one day:** colour resolution split from input, more colours, game configs as ScriptableObjects, configurable spawning, bit-value fix for `BlockColors`, **Color Merge mode, Advanced Color Merge mode, a strategy refactor (`IMergeUtility`), per-mode profiles, mode instructions, main menu**, scene clean-up.
- 01-16 → 01-18 UI scale, point particles, scoring system (on a side branch `points_system`, later triaged as CC-005).

**Burst 2 — polish at weekends, 2026-01-31 → 02-16 (10 commits)**
- 01-31 scoring re-added on master, `ColorProfile` ScriptableObject, a central `GameManager`, UI split from rendering.
- 02-01 jelly and "wobble" shaders. 02-07 switch to forward-rendered 3D.
- 02-15 fix race conditions in UI Toolkit event registration. 02-16 **save system** (`a568c92`). Then nothing for 9 months.

## Era 3 — Claude Code (18 Nov 2026 →)

Per-day: **35 / 45 / 39 / 15 commits** (18th–21st, the 21st partial). 12 PRs merged. Tests **0 → 442 EditMode + 2 PlayMode**. Wiki: 4,587 lines across 35 pages, 5 ADRs, 83 manual test cases.

### Day 1 — Wed 18 Nov: the workflow itself
| Time | Event |
|---|---|
| 10:32–11:12 | Ronan: `CLAUDE.md`, split into user-level + project, wiki folder |
| 11:43 | Planning session writes roadmap CC-001–CC-019 and the CC-001 plan |
| 11:50–12:08 | Three workflow rule changes in 20 minutes (hand-off, local sessions only, Remote Control) |
| 12:10–14:26 | **CC-001** wiki scaffold: 8 system pages, 50 test cases. Reading the AI-era code produces **13 backlog items, CC-020–CC-032** (RNG, spawn, resume, audio, dead code, guards, UI wiring, grid leak, input, save robustness, presentation) |
| 15:09 | PR #1 merged. Quiz → Ronan adds the Burst rule; that commit lands on the branch *after* the squash and is lost (recovered as `8a8eed7`; memory `squash-merge-check`) |
| 15:59 | CC-034 and CC-006 approved; **ADR-001** pure board simulation |
| 16:18–17:06 | **CC-034**: labels, project board, import script (47 issues #3–#49), roadmap moves to GitHub (ADR-002). PR #50 |
| 17:33–18:00 | **CC-033** planned and started: manifest 60 → 17 packages (the `com.unity.ai.*` pre-release set removed first, since it could block resolution), editor upgraded to 6000.6.9f1 in batchmode, FMOD 2.04 as an embedded package (ADR-003) |

### Day 2 — Thu 19 Nov: foundations
| Time | Event |
|---|---|
| 09:36–10:01 | Ronan's own commits: saved FMOD project, Mobile banks, Android build profiles, Unity IAP |
| 12:29 | **CC-033** merged (PR #54, 567 files). 57 `.meta` files re-saved; test runner proven with 0 tests |
| 12:37–17:23 | **CC-006** pure simulation: 4 → 200 → 207 tests; `BoardStepSystem` (Burst) replaces five systems; fixes CC-020–CC-024; resume fixed. PR #56 |
| 17:02–17:05 | Two working copies rule (agent sandbox vs Ronan's editor copy), then moved to `CLAUDE.local.md` |
| 17:55 | Rule: planning session starts feature sessions itself on approval |
| 17:57–19:03 | **CC-003** FMOD clean-up, CLI bank builds. PR #66 |
| 19:11–22:54 | **CC-004** command-line Android APKs. First build **36 min**. Found the APK packed Desktop banks; fixed. Installed on Ronan's Samsung SM-S911B; smoke test passed. PR #67 |
| 23:20–00:31 | **CC-002** Core/Presentation assemblies (ADR-004), 258 → 288 tests, PlayMode smoke tests. PR #78 |

### Day 3 — Fri 20 Nov: fixing Era 2, then speed
| Time | Event |
|---|---|
| 00:36–02:10 | **CC-041** eleven fixes in one branch, no approval stops: CC-025, 027–032, 037, 038, 040, 044. 312 tests. PR #80. Last commit of the night 03:41 |
| 08:26–13:57 | **CC-043** build times: Unity's own tools first (Bee trace, build report, editor log). Defender exclusions, then one setting at a time. One-line dev rebuild **4:33 → 1:49**, clean 31:14 → 13:17, `libil2cpp.so` 352 → 96 MB. PR #84 |
| — | Rules: commit without waiting for long builds; device builds only at PR time; archive sessions after merge + quiz |
| 21:55–22:27 | **CC-039** version codes and **CC-042** rotating saves planned and implemented **in parallel**. PRs #90, #91 |

### Day 4 — Sat 21 Nov (to 11:03)
| Time | Event |
|---|---|
| 07:37–09:39 | #90, #91 merged; save cost measured on the phone: 2.84 ms median per move |
| 09:46–09:52 | Ronan: "Make a plan for 008" → "I approve" (6 minutes) |
| 09:54–10:47 | **CC-008** rules framework (ADR-005): 300 recorded seeded games pin behaviour, refactor proves identical play. 442 tests. PR #96 |
| 11:01–11:03 | **CC-046/CC-047** automated playthroughs (editor and Android device) put first on the roadmap; CC-046 in progress |

## Workflow rule changes (Era 3)

21 `schema` entries in the wiki log in 4 days (8 / 9 / 3 / 1 per day), plus 10 memory files recording Ronan's corrections. The rules settle fast: most churn is on day 1–2.

| Date | Change | Trigger |
|---|---|---|
| 11-18 | User-level / project split | Start |
| 11-18 | Planning session owns roadmap; feature sessions only implement | Start |
| 11-18 | Local sessions only, Remote Control | Cloud sessions can't see `~/.claude/CLAUDE.md` |
| 11-18 | Don't mark as MANUAL what the agent can do | CC-001 plan listed git setup as manual |
| 11-18 | Ask before working around a missing tool | `gh` missing at PR #1 |
| 11-18 | Quiz: why/trade-off questions, one part each | Ronan rejected first quiz |
| 11-18 | Favour Burst-compatible ECS | CC-001 quiz discussion |
| 11-18 | Status lives on the board, never in commits | CC-034 / noise in history |
| 11-18 | Commits readable without the plan | "Plan CC-033: …" style commits |
| 11-18 | Check squash merges against branch tip | Lost commit after PR #1 |
| 11-19 | Two working copies (sandbox vs manual) | Batchmode blocked by open editor |
| 11-19 | Never message other sessions unasked | Planning session briefed CC-033 session unasked |
| 11-19 | Planning session starts feature sessions; merge watcher | Ronan away from the machine |
| 11-19 | `roadmap.md` holds open work only | Strikethrough clutter |
| 11-20 | Don't hold commits for long builds | 20–30 min builds |
| 11-20 | Device builds only at PR time | Iteration cost |
| 11-20 | Archive feature sessions after merge + quiz | (reason not recorded) |
| 11-20 → 21 | "No approval stops" plans (CC-041, 039, 042, 008) | Small, well-specified items |
