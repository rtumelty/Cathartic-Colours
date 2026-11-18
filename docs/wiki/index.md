# Wiki index — Cathartic Colours

Catalogue of every wiki page. Read this first; update it on every change.

## Planning
- [Roadmap](roadmap.md) — release goal, phases, suggested order, and the id-to-issue table. Status and fields live on the [project board](https://github.com/users/rtumelty/projects/1) ([ADR-002](decisions/ADR-002-issue-tracking.md)).

## Features
- [Feature page format](features/README.md) — filename, header table, sections and conventions for feature pages.
- [CC-001 Wiki scaffold and system pages](features/CC-001-wiki-scaffold.md) — plan for the full wiki structure, system pages and seeded test plan. Issue [#3](https://github.com/rtumelty/Cathartic-Colours/issues/3).
- [CC-006 Extract pure board simulation from ECS](features/CC-006-pure-simulation.md) — pure, tested simulation assembly with ECS as a thin wrapper; absorbs CC-020–CC-024. Issue [#12](https://github.com/rtumelty/Cathartic-Colours/issues/12). The page is on `feature/CC-006-pure-simulation`.
- [CC-034 Move roadmap tracking to GitHub Issues and Projects](features/CC-034-github-issues.md) — issues and a Project board for item tracking; the wiki keeps the knowledge pages. Issue [#46](https://github.com/rtumelty/Cathartic-Colours/issues/46).

## Decisions
- [ADR format](decisions/README.md) — when an ADR is needed, numbering, statuses.
- [ADR template](decisions/ADR-000-template.md) — copy this to start a new ADR (ADR-000 is a template, not a decision).
- [ADR-001 Pure board simulation core](decisions/ADR-001-pure-board-simulation.md) — rules move into a pure `Simulation` assembly with struct `*MergeRules` strategies behind `IMergeRules`; one `BoardStepSystem` wraps it. Status: Accepted.
- [ADR-002 Track roadmap items in GitHub Issues and a Project board](decisions/ADR-002-issue-tracking.md) — hybrid tracking: issues, sub-issues and board for status and fields; the wiki keeps knowledge. Status: Accepted.

## Systems
- [System page format](systems/README.md) — how system pages describe current behaviour, cite sources and link known issues.

- [ECS frame order](systems/ecs-frame-order.md): every system, its group and ordering constraints, tags, event entities, singletons, and one move frame by frame.
- [Merge rules](systems/merge-rules.md): merge rules per mode with worked examples, sliding, scoring, spawning and end conditions.
- [Configuration and game start](systems/configuration.md): `GameConfiguration`, `ColorProfile`, their assets, static `GameManager`, and the path from menu to running game.

- [Save system](systems/save-system.md): save schema field by field, when it saves and deletes, the resume hand-off, no versioning.
- [Audio and FMOD](systems/audio-fmod.md): audio event entities, the five FMOD events, FMOD for Unity settings, Studio project layout and the `.unsaved/` hazard.
- [Input](systems/input.md): Input System setup, keyboard and swipe paths, `MoveDirectionComponent` lifecycle.
- [UI layout](systems/ui-layout.md): UXML/USS assets, every queried element name checked against the UXML, panels, UI events, `LayoutObserver` and camera fit.
- [Presentation](systems/presentation.md): `GameRenderer`, `Block`, jelly wobble, score particles, and the ECS state each reads and writes.

## Testing
- [Manual test plan](test-plan.md) — how to run the test plan, case format, 51 current-behaviour cases across 8 game areas (MENU, STD, CM, ACM, HUD, OVER, SAVE, AUDIO), and 4 project-tracking cases (TRACK, CC-034).

## Raw sources
- [Raw sources](../raw/README.md) — immutable source material and its register (PuzzleScript prototype, planning discussion when supplied).

## History
- [Log](log.md) — append-only record of plans, approvals, implementation, decisions and schema changes.
