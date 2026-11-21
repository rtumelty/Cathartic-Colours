# Presentation outline (draft v2)

**Format (Ronan, 2026-11-21):** 30-minute slide talk, engineers and tech artists at Unity, candid.

Working title: **"Three ways to build the same game: PuzzleScript, Unity's AI Assistant, and an agent team"**

Narrative in one line: *each era was fastest at a different thing; the game only became safe to change once the AI had to write down what it knew and prove what it changed — and the price was paid in Ronan's attention and understanding.*

Budget: 13 slides, about 25 minutes, 5 minutes of questions. Minutes per slide in brackets.

| # | Slide | Point | Visual / evidence |
|---|---|---|---|
| 1 | Title + the game (1) | What Cathartic Colours is | 10-second clip from the Android build: a merge in each mode, jelly wobble, score particles |
| 2 | Three eras on one line (1.5) | Same design, three ways to build it, two long gaps | Timeline: Apr 2021 (2 commits) · gap · Jan–Feb 2026 (45) · 9-month gap ("distraction, motivation") · Nov 2026 (134 in 4 days). Commits per day by era (`data/daily-activity.csv`) |
| 3 | Era 1: PuzzleScript (1.5) | The design was right in 252 lines | The .pz rule block next to today's `BoardSimulation.Step`. The prototype's "colour foreshadowing" is still missing from Unity (CC-007) |
| 4 | Era 2: hand-written ECS, then the AI Assistant (2.5) | In-editor AI got a game on screen fast | Ronan's 937-line barebones ECS pass, then the Assistant: 13 commits on 2026-01-15 (two modes, strategy refactor, main menu). **Tech-artist hook:** jelly and wobble shaders, particles, the 3D forward-renderer switch all came from this era |
| 5 | Era 2: what it left behind (2.5) | Speed without structure | Diagram: one move smeared over five systems, simulation calling presentation (ADR-001). 0 tests, everything in `Assembly-CSharp`, FMOD events only in `.unsaved/`, pre-release `com.unity.ai.*` packages that later blocked the upgrade. 17 latent issues found on reading |
| 6 | Restarting after nine months (1.5) | Nobody, human or AI, knew how it worked | CC-001: a day of archaeology, 8 system pages, 13 backlog items in a few hours. Quote ADR-001: "bugs … live in the seams between systems" |
| 7 | Era 3: the workflow (2.5) | Planning session → feature sessions → Haiku/Sonnet/Opus workers → PR → Ronan merges | Diagram of sessions, wiki, board. Rule changes per day (8, 9, 3, 1) — most of the process was invented on days 1–2, from Ronan's corrections |
| 8 | Before/after: the simulation (2.5) | Rules testable without a World, Burst inside one | Five systems + `IMergeUtility` classes → `BoardSimulation.Step<TRules>` with struct rules and one Burst `BoardStepSystem`. Tests 0 → 200 in one feature |
| 9 | Record, then refactor (1.5) | 300 seeded games as a behaviour lock (CC-008) | Test curve 0 → 442 (`data/code-and-tests.csv`). CC-008: plan approved in 6 minutes, PR 18 minutes later, identical play proven |
| 10 | Unity from the command line (2) | Upgrade, test, build, audio, all in batchmode | CC-033 (60 → 17 packages, 6.0 → 6.6 overnight; strip pre-release packages first), `-runTests` gotchas, FMOD CLI banks (Android was packing the Desktop banks), APK pipeline. Build times with Unity's tools first: one-line rebuild 4:33 → 1:49, `libil2cpp.so` 352 → 96 MB (CC-043) |
| 11 | What agents still can't do (1.5) | The editor, the screen, the device | Hub GUI installs, FMOD Studio, listening checks, device smoke tests, scene/prefab wiring. **Tech-artist hook:** no new visuals in Era 3; the aesthetic rework (CC-018) is open. Automated playthroughs (CC-046/047) are the attempt to let agents see the game |
| 12 | Honest costs (2.5) | Overhead, own mistakes, and the human bill | 56% of Era 3 commits docs-only; game code +28% vs test code 0 → 6.7k lines; lost squash commit; version codes going backwards. Ronan: 146 messages in 4 days, 40% just approvals, messages until 02:12; quiz answers with "I don't know" 5 times |
| 13 | What I'd tell you (1.5) | Use each tool for what it's fastest at | Prototype in the cheapest medium. Let in-editor AI get it on screen. Before scaling, give the agent seams, tests and a written memory. Remove approval stops as trust grows, but keep a way to stay close to the code (the quiz). Humans keep merge, device and look-and-feel |

## Charts to build (data in `data/`)
1. Commits per active day by era (split axis: Era 2 peaks at 13, Era 3 at 45).
2. Game code vs test code over time.
3. Plan-to-PR time per feature, against approval stops removed (`feature-cycle-times.csv`).
4. Android build times before/after CC-043.
5. Ronan's messages per day, with the share that are approvals (`ronan-attention.csv`).
6. Workflow rule changes per day.

## Before-and-after examples to pull
- `InputProcessingSystem` calling `JellyManager.StartWobble` (Era 2) vs `MergeEffectsSystem` reacting to events (Era 3).
- `IMergeUtility` / `MergeUtilityFactory` classes vs struct rules + `GameRules` (ADR-001 → ADR-005).
- Non-atomic save write vs rotating, validated saves (CC-031, CC-042).
- Commit messages: `4fb67bd` "Fix issue caused by too frequent input" vs `85a97ef`.
- A Ronan correction and the rule it produced (arcane commits → readable-commit rule).

## Still to decide
- Whether to demo live (risky in 30 minutes) or use recorded clips. Recorded clips are the default.
- Whether Era 2 Assistant transcripts (Monday) change slides 4–5.
