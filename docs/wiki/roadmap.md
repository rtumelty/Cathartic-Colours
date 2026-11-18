# Roadmap — Cathartic Colours

Source of truth for planned work and progress. Ids are `CC-<nnn>` (one sequence for all types, never reused). Deliverables (`CC-<nnn>.<n>`) are added when an item is planned. Field definitions: see user-level `CLAUDE.md` §5.

**Release goal:** Android launch of Cathartic Colours.

## Summary

| Id                | Title                                                        | Type    | Severity / impact | Complexity | Status  | Depends on     |
| ----------------- | ------------------------------------------------------------ | ------- | ----------------- | ---------- | ------- | -------------- |
| [CC-001](#cc-001) | Wiki scaffold and system pages                               | Chore   | Medium            | S          | Backlog | —              |
| [CC-002](#cc-002) | Test infrastructure (assembly definitions + CLI test runs)   | Chore   | High              | M          | Backlog | —              |
| [CC-003](#cc-003) | FMOD events exist only in `.unsaved/`                        | Bug     | High              | S          | Backlog | —              |
| [CC-004](#cc-004) | Android build pipeline                                       | Chore   | High              | M          | Backlog | CC-003         |
| [CC-005](#cc-005) | Triage `points_system` branch                                | Chore   | Low               | S          | Backlog | —              |
| [CC-006](#cc-006) | Extract pure board simulation from ECS                       | Chore   | High              | L          | Backlog | CC-002         |
| [CC-007](#cc-007) | Next-spawn preview                                           | Feature | High              | S          | Backlog | CC-006         |
| [CC-008](#cc-008) | Game mode rules framework                                    | Feature | High              | L          | Backlog | CC-006         |
| [CC-009](#cc-009) | Puzzle mode and generator (unique solutions)                 | Feature | High              | XL         | Backlog | CC-006, CC-008 |
| [CC-010](#cc-010) | Time attack mode                                             | Feature | Medium            | M          | Backlog | CC-008         |
| [CC-011](#cc-011) | Shifting level size mode                                     | Feature | Medium            | M          | Backlog | CC-008         |
| [CC-012](#cc-012) | Environmental hazards                                        | Feature | Medium            | L          | Backlog | CC-008         |
| [CC-013](#cc-013) | Platform Toolkit: auth, cloud saves, achievements            | Feature | High              | L          | Backlog | CC-004         |
| [CC-014](#cc-014) | Social features                                              | Feature | Medium            | L          | Backlog | CC-013         |
| [CC-015](#cc-015) | Ads, attribution and consent                                 | Feature | High              | L          | Backlog | CC-004         |
| [CC-016](#cc-016) | One-time IAP to remove ads                                   | Feature | High              | M          | Backlog | CC-015         |
| [CC-017](#cc-017) | Performance: profiling baseline, Adaptive Performance / ADPF | Feature | Medium            | M          | Backlog | CC-004         |
| [CC-018](#cc-018) | Aesthetic rework of gameplay and UI                          | Feature | High              | XL         | Backlog | —              |
| [CC-019](#cc-019) | Level-complete panel is unreachable                          | Bug     | Low               | S          | Backlog | —              |

### Suggested order
1. **Foundations:** CC-001, CC-003, CC-002, CC-004, CC-005
2. **Core refactor:** CC-006
3. **Gameplay:** CC-007 → CC-008 → CC-009 → CC-010 / CC-011 / CC-012
4. **Platform and monetisation:** CC-013 → CC-014; CC-015 → CC-016
5. **Polish:** CC-017 (profile on device), CC-018 (can run in parallel any time; finalise after the new screens from 3–4 exist)

---

## Foundations

### CC-001
**Wiki scaffold and system pages** · Chore · Medium · S
Create the full `docs/wiki/` structure (index, log, test plan, `features/`, `decisions/`, `systems/`) and `docs/raw/`. Write system pages for existing behaviour: merge rules per mode, ECS frame order, save system, audio/FMOD, input, UI layout. Seed `test-plan.md` with current areas.

### CC-002
**Test infrastructure** · Chore · High · M
Game scripts compile into `Assembly-CSharp`, which test assemblies can't reference. Add assembly definitions for game code (respecting existing namespaces), EditMode and PlayMode test assemblies, one passing test of each kind, and the command-line test run documented in `CLAUDE.md` §4 (Unity path recorded locally).

### CC-003
**FMOD events exist only in `.unsaved/`** · Bug · High · S
The five events (`Block_Move`, `Block_Merge_Small/Medium/Large`, `GameOver`) were never saved into the FMOD project; only `.unsaved/` and the committed `Build/Desktop/Master.bank` contain them. Rebuilding banks before recovery would ship silent builds. Back up `.unsaved/`, recover and save in FMOD Studio (MANUAL), commit `Metadata/Event/`, add an FMOD `.gitignore` (`.cache/`, `.unsaved/`, `.user/`; decide on `Build/`).

### CC-004
**Android build pipeline** · Chore · High · M
Android build profile; real application id and version scheme (Standalone id is still `com.DefaultCompany.2D-URP`); command-line build script (`-executeMethod`) for agent-produced builds; keystore handling kept out of git; consider an FMOD Mobile platform for mobile-appropriate encoding.

### CC-005
**Triage `points_system` branch** · Chore · Low · S
Remote branch `points_system` is unmerged. Determine whether it's superseded by master (scoring already exists) and delete, merge or archive.

## Core refactor

### CC-006
**Extract pure board simulation from ECS** · Chore · High · L
Move slide/merge/spawn rules into a pure, deterministic, Burst-friendly model (e.g. `BoardState` + `Step(board, dir) → (board, events)`), with ECS systems as thin wrappers. Remove presentation coupling from simulation (`InputProcessingSystem` → `JellyManager`, `ScoringSystem` → particle spawner/renderer). Prerequisite for preview, modes, solver/generator and cloud-save robustness. Heavy unit-test coverage.

## Gameplay

### CC-007
**Next-spawn preview** · Feature · High · S
Let players see what will spawn next. Today the indicator's colour is only chosen on the next move (`ResolveNewBlocksSystem`); choose it at spawn time (`SpawnColorSystem`), store it, render it, and keep save/resume consistent.

### CC-008
**Game mode rules framework** · Feature · High · L
`IMergeUtility` covers merge rules only. Generalise `GameMode` into composable rules: spawn policy, end conditions, timers, grid mutations, scoring modifiers. Existing three modes re-expressed on the framework with no behaviour change.

### CC-009
**Puzzle mode and generator** · Feature · High · XL
Puzzle mode built on the shared simulation, with deterministic or no spawns. Generator plus solver (search with state hashing) that accepts puzzles only when they have a unique solution (stop at second solution). Difficulty grading; possible daily seeded puzzle (feeds CC-014).

### CC-010
**Time attack mode** · Feature · Medium · M
Timed mode using the rules framework (timer, time bonuses, end condition, scoring).

### CC-011
**Shifting level size mode** · Feature · Medium · M
Grid dimensions change during play (grow/shrink rules, handling blocks at removed edges, camera/layout response).

### CC-012
**Environmental hazards** · Feature · Medium · L
Board hazards as grid-mutation and rule modules (design to be explored in planning).

## Platform and monetisation

### CC-013
**Platform Toolkit: auth, cloud saves, achievements** · Feature · High · L
Unity Platform Toolkit with Play Games Services v2. Cloud saves reuse the JSON save; note the toolkit resolves conflicts by most-recently-modified. Don't combine with the Google Play Games plugin (manifest conflicts). Achievements defined in Play Console (MANUAL).

### CC-014
**Social features** · Feature · Medium · L
Leaderboards per mode, sharing (e.g. daily puzzle results). Confirm Platform Toolkit leaderboard coverage during planning.

### CC-015
**Ads, attribution and consent** · Feature · High · L
Ad network and attribution SDK integration with GDPR/UMP consent flow built in from the start. Ad placements designed to fit a casual, cathartic game. Keys never committed.

### CC-016
**One-time IAP to remove ads** · Feature · High · M
Unity IAP non-consumable "remove ads", restore purchases, entitlement persisted (and synced via CC-013 where possible).

## Polish

### CC-017
**Performance** · Feature · Medium · M
Device profiling baseline first. Then Adaptive Performance with the Android provider (ADPF hints; use provider ≥1.2, not Pixel-only 1.0) and, likely higher value for a turn-based 2D game, on-demand rendering while idle.

### CC-018
**Aesthetic rework of gameplay and UI** · Feature · High · XL
Complete visual overhaul of gameplay and UI. `GameRenderer` only reads ECS state, so it can proceed in parallel; finalise once new screens (modes, puzzles, store, account) exist.

## Backlog bugs

### CC-019
**Level-complete panel is unreachable** · Bug · Low · S
`GameUI.uxml` has a "Level Complete / Next Level" panel and `GameStateComponent.LevelComplete` exists, but no mode sets it. Decide whether to remove it or use it (e.g. for puzzle mode, CC-009).
