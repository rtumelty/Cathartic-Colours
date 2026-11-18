# Roadmap — Cathartic Colours

Source of truth for planned work and progress. Ids are `CC-<nnn>` (one sequence for all types, never reused). Deliverables (`CC-<nnn>.<n>`) are added when an item is planned. Field definitions: see user-level `CLAUDE.md` §5.

**Release goal:** Android launch of Cathartic Colours.

## Summary

| Id                | Title                                                        | Type    | Severity / impact | Complexity | Status  | Depends on     |
| ----------------- | ------------------------------------------------------------ | ------- | ----------------- | ---------- | ------- | -------------- |
| [CC-001](#cc-001) | Wiki scaffold and system pages                               | Chore   | Medium            | S          | In progress | —              |
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
| [CC-020](#cc-020) | Colour roll doesn't advance the stored RNG                   | Bug     | Medium            | S          | Backlog | —              |
| [CC-021](#cc-021) | Spawn can fail while empty cells remain                      | Bug     | Medium            | S          | Backlog | —              |
| [CC-022](#cc-022) | Resume replays the resolve/spawn step                        | Bug     | High              | S          | Backlog | —              |
| [CC-023](#cc-023) | Move sound plays on rejected moves                           | Bug     | Low               | S          | Backlog | —              |
| [CC-024](#cc-024) | Remove dead ECS code                                         | Chore   | Low               | S          | Backlog | —              |
| [CC-025](#cc-025) | Missing guards on singletons, config and scene name          | Chore   | Low               | S          | Backlog | —              |
| [CC-026](#cc-026) | Review mode rule gaps (Advanced dead ends, Color Merge size) | Chore   | Medium            | S          | Backlog | —              |
| [CC-027](#cc-027) | Runtime configuration leaks between menu, presets and resume | Bug     | Low               | S          | Backlog | —              |
| [CC-028](#cc-028) | In-game UI wiring faults (status label, double restart)      | Bug     | Medium            | S          | Backlog | —              |
| [CC-029](#cc-029) | Restart stacks duplicate grid cells; grid colour not applied | Bug     | Medium            | S          | Backlog | —              |
| [CC-030](#cc-030) | Input uses a hard-coded keyboard action instead of `GameControls` | Chore | Medium          | S          | Backlog | —              |
| [CC-031](#cc-031) | Save file robustness (versioning, atomic write)              | Bug     | Medium            | S          | Backlog | —              |
| [CC-032](#cc-032) | Presentation-layer cleanup (jelly material, unused scripts)  | Chore   | Low               | S          | Backlog | —              |

### Suggested order
1. **Foundations:** CC-001, CC-003, CC-002, CC-004, CC-005
2. **Core refactor:** CC-006
3. **Gameplay:** CC-007 → CC-008 → CC-009 → CC-010 / CC-011 / CC-012
4. **Platform and monetisation:** CC-013 → CC-014; CC-015 → CC-016
5. **Polish:** CC-017 (profile on device), CC-018 (can run in parallel any time; finalise after the new screens from 3–4 exist)

---

## Foundations

### CC-001
**Wiki scaffold and system pages** · Chore · Medium · S · **In progress**
Links: [feature page](features/CC-001-wiki-scaffold.md) · branch `feature/CC-001-wiki-setup` · PR —

| Deliverable | Title | Risk | Worker | Status |
|---|---|---|---|---|
| CC-001.1 | Structure, conventions and raw layer | Low | Haiku | Done |
| CC-001.2 | Core simulation pages (frame order, merge rules, configuration) | Medium | Sonnet | Done |
| CC-001.3 | Supporting system pages (save, audio, input, UI, presentation) | Medium | Sonnet | Done |
| CC-001.4 | Seed test plan and lint | Low/Medium | Sonnet + Haiku | In review |

Create the full `docs/wiki/` structure (index, log, test plan, `features/`, `decisions/`, `systems/`) and `docs/raw/`. Write system pages for existing behaviour: merge rules per mode, ECS frame order, save system, audio/FMOD, input, UI layout. Seed `test-plan.md` with current areas.

### CC-002
**Test infrastructure** · Chore · High · M
Game scripts compile into `Assembly-CSharp`, which test assemblies can't reference. Add assembly definitions for game code (respecting existing namespaces), EditMode and PlayMode test assemblies, one passing test of each kind, and the command-line test run documented in `CLAUDE.md` §4 (Unity path recorded locally).

### CC-003
**FMOD events exist only in `.unsaved/`** · Bug · High · S
The five events (`Block_Move`, `Block_Merge_Small/Medium/Large`, `GameOver`) were never saved into the FMOD project; only `.unsaved/` and the committed `Build/Desktop/Master.bank` contain them. Rebuilding banks before recovery would ship silent builds. Back up `.unsaved/`, recover and save in FMOD Studio (MANUAL), commit `Metadata/Event/`, add an FMOD `.gitignore` (`.cache/`, `.unsaved/`, `.user/`; decide on `Build/`). Current state (2026-11-18): the saved `Metadata/` has no `Event/` folder, while `.unsaved/Metadata/Event/` holds the five events. See [audio-fmod](systems/audio-fmod.md#the-unsaved-hazard).

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
`GameUI.uxml` has a "Level Complete / Next Level" panel and `GameStateComponent.LevelComplete` exists, but no mode sets it. Decide whether to remove it or use it (e.g. for puzzle mode, CC-009). The PuzzleScript prototype declares a win when the board is cleared ([merge-rules](systems/merge-rules.md#end-conditions)).

_CC-020 to CC-027 were found while writing the CC-001.2 system pages (2026-11-18). Details and source citations are in each page's Known issues._

### CC-020
**Colour roll doesn't advance the stored RNG** · Bug · Medium · S
`ResolveNewBlocksSystem` rolls the indicator colour from a local copy of `GameStateComponent` and never writes it back, so `SpawnColorSystem` draws the next position from the same RNG state. Colour and next position are correlated. Matters for CC-007 and seeded puzzles (CC-009). [ecs-frame-order](systems/ecs-frame-order.md#known-issues)

### CC-021
**Spawn can fail while empty cells remain** · Bug · Medium · S
`SpawnColorSystem` makes at most `Width*Height*2` random tries for a cell (about 13% failure with one empty cell on 6x6). On failure it spawns nothing and the move isn't saved. Pick from the list of empty cells instead. [ecs-frame-order](systems/ecs-frame-order.md#known-issues)

### CC-022
**Resume replays the resolve/spawn step** · Bug · High · S
Saves happen while `WaitingForInput` is false, so a resumed game starts mid-move: the first frame resolves the saved indicator and spawns another with no move made. Restored blocks, including the indicator, also get a disabled `MovingBlockTag`. Found by code reading; reproduce first. Relevant to cloud saves (CC-013). [ecs-frame-order](systems/ecs-frame-order.md#known-issues)

### CC-023
**Move sound plays on rejected moves** · Bug · Low · S
`BlockMovementSystem` emits `Block_Move` whenever a move command exists, including during game over. [ecs-frame-order](systems/ecs-frame-order.md#known-issues)

### CC-024
**Remove dead ECS code** · Chore · Low · S
Unused `PendingMergeTag`, `PendingDestructionTag`, `SpawnNextColorRequest`, `GridCellComponent` and the three mode tags; unreachable branches in `BlockMovementSystem`; a no-op copy-back in `ScoringSystem`; `SaveGameSystem` checks for a save file every frame during game over. Can be absorbed by CC-006. [ecs-frame-order](systems/ecs-frame-order.md#known-issues), [merge-rules](systems/merge-rules.md#known-issues)

### CC-025
**Missing guards on singletons, config and scene name** · Chore · Low · S
`GameEndConditionSystem` reads singletons it doesn't require. `GameManager.Initialize` throws on a null colour profile. `MainMenuController.gameSceneName` is "Gameplay Scene" but the file is `Gameplay scene.unity` (case differs; whether `LoadScene` tolerates it is unverified). `GameRenderer.CleanUp` (also called from `OnDestroy`) uses `entityManager` even if `InitializeGame` never ran. [ecs-frame-order](systems/ecs-frame-order.md#known-issues), [configuration](systems/configuration.md#known-issues), [presentation](systems/presentation.md#known-issues)

### CC-026
**Review mode rule gaps** · Chore · Medium · S
Advanced Color Merge has dead ends: same-colour Mediums, Small+Medium and different-colour Smalls never merge. Color Merge ignores size and uses the moving block's size. Design input for CC-008. [merge-rules](systems/merge-rules.md#known-issues)

### CC-027
**Runtime configuration leaks between menu, presets and resume** · Bug · Low · S
`LoadPreset` doesn't copy tier points. The menu doesn't expose `spawnNextColourSystem`. Resuming replaces the shared runtime config, so the menu then shows the resumed game's settings. [configuration](systems/configuration.md#known-issues)

_CC-028 to CC-032 were found while writing the CC-001.3 system pages (2026-11-18)._

### CC-028
**In-game UI wiring faults** · Bug · Medium · S
`GameUI.uxml` has no `status-label`, so the status text never shows. `restart-button` appears twice: the game-over button is hooked twice, so one click raises `OnRestartRequested` twice, and the level-complete button is never hooked. `UnhookEvents` unsubscribes fresh lambdas, so it removes nothing. Found by code reading. [ui-layout](systems/ui-layout.md#known-issues)

### CC-029
**Restart stacks duplicate grid cells; grid colour never applied** · Bug · Medium · S
`GameRenderer.CleanUp` doesn't destroy the `Cell` objects, so each restart adds another full grid (two per click, because of CC-028). `CreateVisualGrid` tints a `SpriteRenderer`, but the `Cell` prefab only has a `MeshRenderer`, so `ColorProfile.gridBackgroundColor` is ignored. [presentation](systems/presentation.md#known-issues)

### CC-030
**Input uses a hard-coded keyboard action instead of `GameControls`** · Chore · Medium · S
`GameInputHandler` builds its own keyboard-only `Move` action and ignores the project-wide `GameControls.inputactions`, so gamepads don't work. `SwipeDetector` is touch-only, so it can't be tested with a mouse in the editor. Dead-zone rules differ between keyboard and swipe. `SwipeDetector` is in the nested namespace `Gameplay.CatharticColours`. [input](systems/input.md#known-issues)

### CC-031
**Save file robustness** · Bug · Medium · S
`version` and `saveTimestamp` are never read (no migration or rejection). The file is written in place, so a crash mid-write can leave it truncated. `colorProfileName` is saved but ignored. Do this before cloud saves (CC-013). [save-system](systems/save-system.md#known-issues)

### CC-032
**Presentation-layer cleanup** · Chore · Low · S
`JellyManager` writes to the shared `BlockMaterial.mat` asset, which changes it during editor play, and its smoothing depends on frame rate. `JellyController` is unused. `ScoreParticles.prefab` has stale fields and empty digit systems, so Text mode would throw. The Advanced Color Merge top-tier clear emits transparent particles (result colour `None`). [presentation](systems/presentation.md#known-issues)
