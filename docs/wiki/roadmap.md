# Roadmap — Cathartic Colours

Strategy page: the release goal, the phases and the suggested order. **Item tracking lives on GitHub** (since CC-034, [ADR-002](decisions/ADR-002-issue-tracking.md)):

- **Board:** [Cathartic Colours Roadmap](https://github.com/users/rtumelty/projects/1). It holds each item's Status (Backlog → Planning → Approved → In progress → In review → Done) and its Severity, Size and Phase.
- **Issues:** one per item, titled `CC-nnn: <title>`, with labels `type:*`, `severity:*` and `size:*`. The body has the description, wiki links and `Depends on`. Deliverables (`CC-nnn.n`) are sub-issues.
- **Views:** Board (by Status), Backlog by severity, Bugs.
- **New items:** see project `CLAUDE.md` §6. The next id is one more than the highest `CC-` issue title.

This page doesn't repeat any status or field. Change those on the board, not here.

**Release goal:** Android launch of Cathartic Colours.

## Phases and suggested order
1. **Foundations:** ~~[CC-001](https://github.com/rtumelty/Cathartic-Colours/issues/3)~~, ~~[CC-034](https://github.com/rtumelty/Cathartic-Colours/issues/46)~~ (tracking), ~~[CC-033](https://github.com/rtumelty/Cathartic-Colours/issues/45)~~ (Unity 6.6), then [CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9) (FMOD clean-up and Studio 2.04), [CC-004](https://github.com/rtumelty/Cathartic-Colours/issues/10), [CC-005](https://github.com/rtumelty/Cathartic-Colours/issues/11)
2. **Core refactor:** [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12) (absorbs [CC-020](https://github.com/rtumelty/Cathartic-Colours/issues/32)–[CC-024](https://github.com/rtumelty/Cathartic-Colours/issues/36) and the simulation part of CC-002), then [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8) (rest)
3. **Quick fixes, any time:** [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40) + [CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41) together (restart/UI); [CC-031](https://github.com/rtumelty/Cathartic-Colours/issues/43) before CC-013. The other small backlog items ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31), [CC-025](https://github.com/rtumelty/Cathartic-Colours/issues/37), [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38), [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39), [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42), [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)) sit in this phase on the board until they're scheduled.
4. **Gameplay:** [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19) → [CC-008](https://github.com/rtumelty/Cathartic-Colours/issues/20) → [CC-009](https://github.com/rtumelty/Cathartic-Colours/issues/21) → [CC-010](https://github.com/rtumelty/Cathartic-Colours/issues/22) / [CC-011](https://github.com/rtumelty/Cathartic-Colours/issues/23) / [CC-012](https://github.com/rtumelty/Cathartic-Colours/issues/24)
5. **Platform and monetisation:** [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25) → [CC-014](https://github.com/rtumelty/Cathartic-Colours/issues/26); [CC-015](https://github.com/rtumelty/Cathartic-Colours/issues/27) → [CC-016](https://github.com/rtumelty/Cathartic-Colours/issues/28)
6. **Polish:** [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29) (profile on device), [CC-018](https://github.com/rtumelty/Cathartic-Colours/issues/30) (can run in parallel any time; finalise after the new screens from 4–5 exist)

## Id to issue

Every roadmap id and its issue. Deliverables are sub-issues of their item. Feature pages are linked from the issue body and from [index.md](index.md).

| Id | Issue | Title |
|---|---|---|
| CC-001 | [#3](https://github.com/rtumelty/Cathartic-Colours/issues/3) | Wiki scaffold and system pages |
| CC-002 | [#8](https://github.com/rtumelty/Cathartic-Colours/issues/8) | Test infrastructure: game-code asmdefs + PlayMode tests |
| CC-003 | [#9](https://github.com/rtumelty/Cathartic-Colours/issues/9) | FMOD project clean-up and Studio 2.04 migration |
| CC-004 | [#10](https://github.com/rtumelty/Cathartic-Colours/issues/10) | Android build pipeline |
| CC-005 | [#11](https://github.com/rtumelty/Cathartic-Colours/issues/11) | Triage `points_system` branch |
| CC-006 | [#12](https://github.com/rtumelty/Cathartic-Colours/issues/12) | Extract pure board simulation from ECS |
| CC-007 | [#19](https://github.com/rtumelty/Cathartic-Colours/issues/19) | Next-spawn preview |
| CC-008 | [#20](https://github.com/rtumelty/Cathartic-Colours/issues/20) | Game mode rules framework |
| CC-009 | [#21](https://github.com/rtumelty/Cathartic-Colours/issues/21) | Puzzle mode and generator (unique solutions) |
| CC-010 | [#22](https://github.com/rtumelty/Cathartic-Colours/issues/22) | Time attack mode |
| CC-011 | [#23](https://github.com/rtumelty/Cathartic-Colours/issues/23) | Shifting level size mode |
| CC-012 | [#24](https://github.com/rtumelty/Cathartic-Colours/issues/24) | Environmental hazards |
| CC-013 | [#25](https://github.com/rtumelty/Cathartic-Colours/issues/25) | Platform Toolkit: auth, cloud saves, achievements |
| CC-014 | [#26](https://github.com/rtumelty/Cathartic-Colours/issues/26) | Social features |
| CC-015 | [#27](https://github.com/rtumelty/Cathartic-Colours/issues/27) | Ads, attribution and consent |
| CC-016 | [#28](https://github.com/rtumelty/Cathartic-Colours/issues/28) | One-time IAP to remove ads |
| CC-017 | [#29](https://github.com/rtumelty/Cathartic-Colours/issues/29) | Performance: profiling baseline, Adaptive Performance / ADPF |
| CC-018 | [#30](https://github.com/rtumelty/Cathartic-Colours/issues/30) | Aesthetic rework of gameplay and UI |
| CC-019 | [#31](https://github.com/rtumelty/Cathartic-Colours/issues/31) | Level-complete panel is unreachable |
| CC-020 | [#32](https://github.com/rtumelty/Cathartic-Colours/issues/32) | Colour roll doesn't advance the stored RNG |
| CC-021 | [#33](https://github.com/rtumelty/Cathartic-Colours/issues/33) | Spawn can fail while empty cells remain |
| CC-022 | [#34](https://github.com/rtumelty/Cathartic-Colours/issues/34) | Resume replays the resolve/spawn step |
| CC-023 | [#35](https://github.com/rtumelty/Cathartic-Colours/issues/35) | Move sound plays on rejected moves |
| CC-024 | [#36](https://github.com/rtumelty/Cathartic-Colours/issues/36) | Remove dead ECS code |
| CC-025 | [#37](https://github.com/rtumelty/Cathartic-Colours/issues/37) | Missing guards on singletons, config and scene name |
| CC-026 | [#38](https://github.com/rtumelty/Cathartic-Colours/issues/38) | Review mode rule gaps (Advanced dead ends, Color Merge size) |
| CC-027 | [#39](https://github.com/rtumelty/Cathartic-Colours/issues/39) | Runtime configuration leaks between menu, presets and resume |
| CC-028 | [#40](https://github.com/rtumelty/Cathartic-Colours/issues/40) | In-game UI wiring faults (status label, double restart) |
| CC-029 | [#41](https://github.com/rtumelty/Cathartic-Colours/issues/41) | Restart stacks duplicate grid cells; grid colour not applied |
| CC-030 | [#42](https://github.com/rtumelty/Cathartic-Colours/issues/42) | Input uses a hard-coded keyboard action instead of `GameControls` |
| CC-031 | [#43](https://github.com/rtumelty/Cathartic-Colours/issues/43) | Save file robustness (versioning, atomic write) |
| CC-032 | [#44](https://github.com/rtumelty/Cathartic-Colours/issues/44) | Presentation-layer cleanup (jelly material, unused scripts) |
| CC-033 | [#45](https://github.com/rtumelty/Cathartic-Colours/issues/45) | Upgrade project to Unity 6000.6.9f1 |
| CC-034 | [#46](https://github.com/rtumelty/Cathartic-Colours/issues/46) | Move roadmap tracking to GitHub Issues and Projects |
