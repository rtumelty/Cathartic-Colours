# CLAUDE.md — Cathartic Colours

This file is the **schema** for this repository's LLM wiki and the working agreement for every agent session. Read it fully before doing anything else. Ronan (repo owner) and agents refine it over time; changes to it are logged in `docs/wiki/log.md`.

---

## 1. Project at a glance

- **Game:** Cathartic Colours — casual 2048-style colour-merge puzzle game. Target: **Android release**.
- **Repo layout**
  - `Cathartic Colours/` — Unity project (Unity **6000.3.2f1**, URP 2D, Entities 1.4.4 / DOTS ECS, Input System, UI Toolkit, FMOD for Unity)
  - `Cathartic Colours - FMOD/` — FMOD Studio 2.03 project (Unity reads banks from its `Build/` folder)
  - `CatharticColours.pz` — original PuzzleScript prototype (reference only)
  - `docs/wiki/` — LLM-maintained wiki (plans, roadmap, decisions, test plan)
  - `docs/raw/` — immutable source material (design notes, external references). Agents read, never edit.
- **Asset serialization:** Force Text. Always commit `.meta` files with their assets.

## 2. Workflow (applies to all feature and bug work)

### 2.1 Planning phase (before any implementation)
1. Explore approaches, impact, risks, architectural decisions and coding patterns.
2. **Favour patterns already in the codebase** (see §6) over introducing new styles. Any new pattern needs a decision page.
3. Break significant work into **small deliverables, each giving incremental value**, in execution order.
4. Every deliverable has, at planning time:
   - a **Definition of Done** (start from the template in §3 and add specifics)
   - the **unit tests and system tests** (Unity Test Framework, EditMode/PlayMode) it adds
   - a **risk tag and assigned worker model** (see §7)
   - any **manual steps for Ronan in other tools** (Unity Editor scene/prefab wiring, FMOD Studio, Play Console, ad dashboards…), clearly marked `MANUAL (Ronan)`
5. The plan is written to `docs/wiki/features/<id>-<slug>.md` and added to the roadmap. Ronan approves it before any code is written.

### 2.2 Hand-off
- After approval, the planning agent gives Ronan a **kickoff prompt** for a new, dedicated session (same group). Ronan starts that session.
- A feature session works **only on its feature**. If asked anything unrelated, call it out and ask for confirmation before proceeding. Unrelated bugs/ideas found during work go into the roadmap **backlog** (with an id), not into the branch.

### 2.3 Implementation phase
1. Branch `feature/<id>-<slug>` from `develop`. All work for the feature happens there.
2. Work through deliverables **in order**. One deliverable → review → commit → summary → **wait for Ronan's approval** before the next.
3. **Sub-agents:** use cheaper models where the model-use policy (§7) allows; the primary model reviews all sub-agent output before it is committed.
4. **Divergence:** the supervising agent flags to Ronan whenever a worker diverges from the agreed plan. Minor implementation specifics → record on the feature page. Anything changing scope, architecture or deliverable order → **stop and get approval**.
5. After each commit: a brief summary of what was done and how it serves the current deliverable.
6. Keep the wiki current as part of every deliverable (feature page, decisions, systems pages, index, log, roadmap status).

### 2.4 Feature completion
1. Produce an **Android build** for device testing.
2. Write a **feature test plan** giving the shortest path to test the work, and merge its cases into the comprehensive `docs/wiki/test-plan.md` (see §5).
3. Open a **pull request** `feature/<id>-<slug>` → `develop` for holistic review. **Ronan owns merging** and does manual device testing before approving.
4. **Knowledge check quiz** (keeps Ronan close to the codebase): present a short quiz of **3–5 questions** about the feature's implementation — design decisions, coding patterns used, where things live, how systems interact. Ask the questions, then wait for Ronan's answers. For each wrong answer, give a brief **one-paragraph** explanation of what was incorrect and what the correct answer is. Correct answers need no explanation.

## 3. Definition of Done — template (extend per deliverable)
- [ ] Code compiles with no new errors or warnings
- [ ] New/changed logic covered by EditMode tests; system behaviour covered by PlayMode/system tests where relevant
- [ ] Full test suite passes via command line (§8)
- [ ] Follows existing patterns (§6); no new pattern without a decision page
- [ ] `MANUAL (Ronan)` steps listed with exact instructions
- [ ] Wiki updated: feature page (status, divergences, specifics), affected systems pages, `index.md`, `log.md`, `roadmap.md`
- [ ] Reviewed by primary model; committed per §4

## 4. Git conventions
- **Branches:** `master` = current stable release · `develop` = active development · `feature/<id>-<slug>` per roadmap item, branched from `develop`.
- **Commits:** subject = short outline of what the commit does (imperative, ≤72 chars). Body = more detail, concise, **max 200 words**.
- **PRs:** one per feature, into `develop`, opened at feature completion. Ronan merges. Agents never merge or push to `master`/`develop` directly.
- **Never commit secrets:** signing keystores (`*.keystore`, `*.jks`), passwords, ad network/attribution keys, service credentials. Keep them in local, gitignored config; document *where* they go, not their values.

## 5. Wiki (llm-wiki model)

Layers: `docs/raw/` (immutable sources) → `docs/wiki/` (agent-maintained pages) → this file (schema).

```
docs/wiki/
  index.md            catalogue of every page: link + one-line summary, by category. Update on every change; read first.
  log.md              append-only history. Entry prefix: "## [YYYY-MM-DD] <op> | <id or page> | <summary>"
                      ops: plan, approve, implement, diverge, decision, ingest, lint, schema
  roadmap.md          source of truth for planned work and progress (format below)
  test-plan.md        comprehensive manual test plan, by area
  features/<id>-<slug>.md   plan, deliverables + DoD, tests, MANUAL steps, divergences, feature test plan, status
  decisions/ADR-<nnn>-<slug>.md   architectural decisions: context, options, decision, consequences
  systems/<name>.md   how existing systems work (merge rules, ECS frame order, save system, audio/FMOD, input, UI)
```

### Roadmap items
Each item has a unique id `CC-<nnn>` (shared sequence for features and bugs, never reused) and:

| Field | Values |
|---|---|
| Type | Feature / Bug / Chore |
| Severity / impact | Critical / High / Medium / Low |
| Complexity | S / M / L / XL |
| Status | Backlog / Planning / Approved / In progress / In review / Done |
| Links | feature page, branch, PR |

Deliverables are listed under their item as `CC-<nnn>.<n>` with their own status.

### Test plan (`test-plan.md`)
- Organised **by area** (e.g. Main menu, each game mode, Gameplay HUD, Game over, Save/Resume, Settings, Audio…).
- Within each area, order scenarios so a tester can run them in one pass without retreading paths (each scenario starts where the previous one ended where possible). Each case: id, preconditions, steps, expected result, related roadmap id.
- Every feature's test cases are folded in at feature completion.

### Lint
Periodically (or when asked) check for contradictions, stale claims, orphan pages, missing cross-links and roadmap/page status mismatches; log the pass.

## 6. Codebase patterns to follow
- **Namespaces:** `ECS.Components`, `ECS.Systems`, `ECS.Utilities`, `Gameplay`, `Data`, `SaveSystem`.
- **ECS:** `partial struct` systems implementing `ISystem`, ordered with `[UpdateInGroup]`/`[UpdateAfter]`/`[UpdateBefore]`; singleton components for game state (`GameStateComponent`, `ScoreComponent`, `GridConfigComponent`, `GameModeComponent`); **tag components** to enable systems (`SpawnColorSystemTag`, mode tags); **event entities** for one-off effects (`AudioEventComponent`, `MergeEventComponent`) consumed and destroyed by their system; `EntityCommandBuffer` for structural changes.
- **Rules as strategies:** merge rules implement `IMergeUtility`, chosen by `MergeUtilityFactory` from `GameMode`.
- **Configuration as ScriptableObjects:** `GameConfiguration`, `ColorProfile` (menu `CatharticColors/…`); runtime copies held by static `GameManager`.
- **Presentation** (MonoBehaviours: `GameRenderer`, `GameUIManager`, `JellyManager`, `PointsParticleSpawner`) reads ECS state; UI is UI Toolkit (UXML/USS), elements found by name with `root.Q<>()`.
- **Audio:** FMOD one-shots by event path constants in `FMODEventPaths`.
- **Save:** JSON via `SaveGameManager` (`persistentDataPath/savegame.json`).

## 7. Model use for sub-agents (split by risk)
- At planning time, every deliverable — and, where useful, each task within it — gets a **risk tag** and an **assigned worker model**. The routing is part of what Ronan signs off when approving the plan.
- Default routing (adjust per plan, with reasons):

| Risk | Typical work | Worker model |
|---|---|---|
| Low | Code search, boilerplate, test scaffolding, wiki/roadmap housekeeping, docs | Haiku |
| Medium | Implementation against a clear spec, tests for existing behaviour, contained refactors | Sonnet |
| High | Core game logic/simulation, ECS system ordering, save formats, cross-cutting architecture, anything hard to reverse | Primary model (no delegation) |

- The primary model always owns planning, design decisions and review; it never delegates review.
- If a worker's output is rejected twice, escalate the task one tier and note it in the feature page.
- Any change to an approved routing is a divergence (§2.3.4) and is flagged to Ronan.

## 8. Running tests and builds
- **Tests (command line):**
  `"<UnityEditorPath>\Unity.exe" -batchmode -projectPath "Cathartic Colours" -runTests -testPlatform EditMode -testResults "TestResults\editmode.xml" -logFile -`
  then the same with `-testPlatform PlayMode`. (Do not pass `-quit` with `-runTests`.) Record the local Unity path in a gitignored file or here once known.
- **Known constraint:** game scripts currently compile into `Assembly-CSharp`, which test assemblies can't reference. Adding assembly definitions for game code is a prerequisite for automated tests.
- **Android build:** no Android build profile or build script is committed yet. Until one exists, builds are a `MANUAL (Ronan)` step.

## 9. Editor-only and external-tool work
- Prefer code plus written instructions over agent edits to scenes, prefabs, `.uxml` layouts with complex wiring, or FMOD Studio metadata. Mark such steps `MANUAL (Ronan)` with exact click paths.
- **FMOD:** always **save the FMOD project** (Ctrl+S) before building banks or committing. Unsaved changes live in `.unsaved/` and are not part of the project. (As of Nov 2026 the game's five events exist only in `.unsaved/` — restore and save them before any audio work.)
