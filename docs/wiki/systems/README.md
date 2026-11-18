# System pages

A system page describes how one part of the game works **today**. It does not describe how it should work; plans and proposed changes belong in feature pages.

## Rules

- **Cite sources.** Every behavioural claim names the file it comes from, as a path under `Assets/Scripts/...` in the Unity project, for example "Each merge is scored once (`Assets/Scripts/ECS/Systems/ScoringSystem.cs`)."
- **Check against code.** Verify claims against the current source before writing. Re-check when a feature changes the system.
- **Known issues are linked.** Problems go in *Known issues*, each linked to its roadmap id, for example [CC-006](../roadmap.md#cc-006). A new problem found while documenting becomes a backlog item in the roadmap; it is not fixed in the wiki pass.
- **Link to tests.** Related test cases in [test-plan.md](../test-plan.md) are linked by id.

## Sections

1. **Purpose:** what the system does for the game, in two or three sentences.
2. **Key files:** a list of the main classes, components and assets, with paths.
3. **How it works:** the current behaviour, in order, with source citations.
4. **Interactions with other systems:** what it reads, writes, triggers or depends on, linking to the other system pages.
5. **Known issues:** each entry gives the problem, the source citation, and the roadmap id.
6. **Related test cases:** test case ids from [test-plan.md](../test-plan.md).

## Pages

Written in CC-001 (core simulation and supporting systems), and listed here as plain text until they exist:

- ecs-frame-order
- merge-rules
- configuration
- save-system
- audio-fmod
- input
- ui-layout
- presentation

Pages that exist are listed in [index.md](../index.md).
