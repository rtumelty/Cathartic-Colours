# CC-001 — Wiki scaffold and system pages

| Field | Value |
|---|---|
| Type | Chore |
| Severity / impact | Medium |
| Complexity | S |
| Status | In review |
| Branch | `feature/CC-001-wiki-setup` (from `develop`) |
| PR | — |
| Roadmap | [CC-001](../roadmap.md#cc-001) |

## Goal
Give every later feature a complete, accurate wiki to plan against: the full folder structure, a raw-sources layer, system pages that describe the game **as it works today**, and a seeded manual test plan.

## Scope
- **In:** wiki structure and conventions, `docs/raw/` with the existing sources registered, system pages for current behaviour, a seeded `test-plan.md`, an ADR template, and a first lint pass.
- **Out:** any change to game code, scenes or FMOD. Behaviour that looks wrong while documenting becomes a **backlog item** (new `CC-<nnn>`), not a fix. Planning any other roadmap item.

## Approach
- System pages are written **from the code**: every behavioural claim cites the file it comes from (`Assets/Scripts/...`), so later sessions can check them.
- Pages describe what *is*, not what should be. Known problems go in a "Known issues" section linked to the roadmap id (for example presentation coupling → CC-006, spawn colour chosen late → CC-007, unreachable level-complete panel → CC-019).
- Existing patterns only. The one new convention is the ADR page template (see D1). It's documentation format, not code, so it gets no ADR of its own.

## Deliverables

### CC-001.1 — Structure, conventions and raw layer
**Risk:** Low · **Worker:** Haiku · **Review:** primary

Creates:
- `docs/wiki/features/`, `docs/wiki/decisions/`, `docs/wiki/systems/` (each with a short `README.md` stating the page format)
- `docs/wiki/decisions/ADR-000-template.md`: Context / Options / Decision / Consequences / Status
- `docs/raw/README.md`: what raw sources are (immutable, read-only) and a register of current sources. Initially: the PuzzleScript prototype `CatharticColours.pz` (referenced in place at the repo root, not moved) and the initial planning discussion that produced the roadmap, if Ronan supplies it (see MANUAL).
- `docs/wiki/test-plan.md` skeleton: a "how to use" section, case format (`TP-<area>-<nn>`, preconditions, steps, expected, roadmap id) and empty sections for the areas in project `CLAUDE.md` §2
- `index.md` rewritten into categories (Planning · Features · Decisions · Systems · Testing · Raw sources · History)

**Definition of Done**
- [x] Template DoD items, where applicable (no code, so no compile/test items, see Tests)
- [x] All folders exist with READMEs; ADR template present
- [x] `test-plan.md` has every area from project `CLAUDE.md` §2
- [x] Index lists every page; log entry added (`implement | CC-001.1`)

_Specifics (2026-11-18):_ system pages cite paths relative to the Unity project folder (`Assets/Scripts/...`, i.e. `Cathartic Colours/Assets/Scripts/...` from the repo root). `docs/raw/` did not exist before; only `CatharticColours.pz` is registered. Relative-link check: 0 broken.

### CC-001.2 — Core simulation pages
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary, checked line by line against code

- `systems/ecs-frame-order.md`: every system in `ECS/Systems/`, with its group and `[UpdateAfter]`/`[UpdateBefore]` constraints, the resolved order as a diagram/list, enabling tags (`SystemFlagComponents`, mode tags), event entities created and consumed, and where structural changes happen (ECB)
- `systems/merge-rules.md`: Standard, Color Merge and Advanced Color Merge rules from `IMergeUtility` implementations and `MergeUtilityFactory`. Includes worked examples per mode, scoring (`ScoringSystem`, `ScoringComponents`), spawn behaviour (`SpawnColorSystem`, `ResolveNewBlocksSystem`) and end conditions (`GameEndConditionSystem`)
- `systems/configuration.md`: `GameConfiguration`, `ColorProfile`, static `GameManager` runtime copies and how a game is started

**Definition of Done**
- [x] Each claim cites its source file; primary has verified the frame order against the attributes in code
- [x] At least one worked example per mode in `merge-rules.md`
- [x] Known issues linked to roadmap ids; any newly found problems added to the roadmap backlog
- [x] Index, log updated

_Specifics (2026-11-18):_ primary re-read all ten systems, the three merge utilities, the components, `GameRenderer.InitializeGame` and `SaveGameManager.RestoreWorldState`, and checked every worked example by hand. 15 new findings were grouped into 8 backlog items (CC-020 to CC-027) rather than one id each. One-cell-per-move movement was flagged by the worker as possibly unintended; it matches the PuzzleScript prototype, so it's documented as design, not as an issue. The D3 update to project `CLAUDE.md` §2 is done in CC-001.3, once all system pages exist.

### CC-001.3 — Supporting system pages
**Risk:** Medium · **Worker:** Sonnet · **Review:** primary

- `systems/save-system.md`: `SaveData` schema (field by field), `SaveGameManager`, `SaveGameSystem` trigger points, file location, version/migration handling (or its absence)
- `systems/audio-fmod.md`: `AudioSystem`, `AudioEventComponent`, `FMODEventPaths`, the five events, bank location (`Cathartic Colours - FMOD/Build/`), and the `.unsaved/` hazard (→ CC-003)
- `systems/input.md`: Input System setup, `GameInputHandler`, `SwipeDetector`, `InputProcessingSystem`, `MoveDirectionComponent` lifecycle
- `systems/ui-layout.md`: `MainMenu.uxml`, `GameUI.uxml/.uss`, named elements queried by `GameUIManager`/`MainMenuController`, panels and how they're shown, `LayoutObserver`
- `systems/presentation.md`: `GameRenderer`, `Block`, `JellyManager`/`JellyController`, `PointsParticleSpawner`, and what ECS state each reads

**Definition of Done**
- [x] Same citation and known-issues rules as CC-001.2
- [x] `ui-layout.md` lists every element name the C# code queries, matched to the UXML
- [x] Index, log updated

_Specifics (2026-11-18):_ primary checked the bug-level findings directly in code: the missing `status-label`, the double restart hook, the no-op `UnhookEvents`, the `Cell` grid surviving `CleanUp`, the `SpriteRenderer` lookup on a mesh-only prefab, `JellyManager` writing to a serialised material, and `GameControls` being registered but unused. New findings became CC-028 to CC-032. The FMOD `Metadata/Event/` finding was folded into CC-003. The merge-sound-by-input-size observation is recorded as design (confirmed by Ronan: a naming choice). Corrected `configuration.md` (grid colour isn't applied) and the FMOD `AutomaticEventLoading` description. D3 done: project `CLAUDE.md` §2 lists the eight system pages. Unverified claims stay marked "not run" on the pages: the built banks containing the events, `PlayerInput` with late-assigned actions, and the visible effect of the double restart.

### CC-001.4 — Seed test plan and lint
**Risk:** Low (test cases Medium) · **Worker:** Sonnet for test cases, Haiku for the lint pass · **Review:** primary

- Fill `test-plan.md` with cases for **current** behaviour in every area, ordered so one tester runs each area in a single pass. Cases come from the system pages.
- Lint pass (user-level §5): broken links, orphan pages, missing cross-links (systems ↔ roadmap ids), status mismatches. Log as `lint`.
- Update the roadmap: CC-001 → In review; add any new backlog items found.

**Definition of Done**
- [x] Every area in `test-plan.md` has at least one case; each case has id, preconditions, steps, expected, roadmap id (`—` for existing behaviour)
- [x] Lint pass logged with findings and fixes; zero broken relative links
- [x] Index, log, roadmap updated

_Specifics (2026-11-18):_ 51 cases (MENU 6, STD 9, CM 6, ACM 7, HUD 6, OVER 6, SAVE 6, AUDIO 5), with a shared notation paragraph before the first area. Seven cases describe desired behaviour for known issues and say "Known to fail until <id>" (CC-030, CC-028, CC-022, CC-023, CC-003, CC-032, CC-029). TP-OVER-05 (editor-only cell count, CC-029) was added at Ronan's request after review; the old TP-OVER-05 became TP-OVER-06. Primary review corrected the particle colour, wobble length, restart notes and coordinates against code. Review also found that the Advanced Color Merge top-tier clear emits transparent particles (folded into CC-032) and that `configuration.md` described a header restart button that doesn't exist (corrected). Lint: see log. Approved by Ronan 2026-11-18; CC-001 moved to In review with the PR.

## Tests
This is a documentation-only change, so no unit or system tests are added, and the "compiles / test suite passes" DoD items don't apply. Verification instead:
- A link check run by the agent (every relative link in `docs/wiki/` resolves), recorded in the CC-001.4 log entry
- Primary-model review of every system page against the cited code
- Ronan reads the system pages in the PR

## Risk and model routing (for approval)
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-001.1 | Low | Haiku | Boilerplate structure and templates |
| CC-001.2 | Medium | Sonnet | Accuracy matters: future plans for CC-006/007/008 rely on these pages. Primary verifies frame order personally. |
| CC-001.3 | Medium | Sonnet | |
| CC-001.4 | Low / Medium | Sonnet (cases), Haiku (lint) | |

Primary model owns all review and commits.

## MANUAL (Ronan)
1. **Optional raw source.** If you have the original planning discussion that produced the roadmap (notes or a chat export), drop it in `docs/raw/` and the session will register it.
2. **Review.** Read the system pages in the PR before merging. They become the reference for every later plan.

_Environment (done by the planning session, 2026-11-18): Git for Windows 2.56 installed, global `core.sshCommand` → Windows OpenSSH; `develop` confirmed in sync with `origin`._

## Decisions
All confirmed by Ronan 2026-11-18.
- **D1:** `ADR-000-template.md` is a template, not a decision, so it doesn't take ADR-001. Real ADRs start at `ADR-001`.
- **D2:** `CatharticColours.pz` stays at the repo root and is registered in `docs/raw/README.md` rather than moved, so existing references don't break.
- **D3:** Two system pages beyond the six listed in project `CLAUDE.md` §2 (`configuration`, `presentation`); scoring, spawn and end conditions are folded into `merge-rules`. _(Wording clarified 2026-11-18 at Ronan's request; the decision is unchanged.)_ Project `CLAUDE.md` §2 is updated to match (log op `schema`).

## Divergences
- 2026-11-18 (minor): plan and roadmap named the branch `feature/CC-001-wiki-scaffold`; the real branch is `feature/CC-001-wiki-setup`. References corrected; feature page filename unchanged.

## Feature test plan
Docs-only: Ronan opens `docs/wiki/index.md` in Obsidian, follows every link, and spot-checks two system pages against the code.

## Status log
- 2026-11-18: Planned
- 2026-11-18: Approved by Ronan (plan, D1–D3, model routing)
- 2026-11-18: In progress (feature session started; develop merged in)
- 2026-11-18: In review (CC-001.1–.4 approved; PR opened to `develop`)
