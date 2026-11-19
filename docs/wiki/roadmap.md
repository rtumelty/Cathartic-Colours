# Roadmap — Cathartic Colours

Strategy page: the release goal, the phases and the suggested order. **Item tracking lives on GitHub** (since CC-034, [ADR-002](decisions/ADR-002-issue-tracking.md)):

- **Board:** [Cathartic Colours Roadmap](https://github.com/users/rtumelty/projects/1). It holds each item's Status (Backlog → Planning → Approved → In progress → In review → Done) and its Severity, Size and Phase.
- **Issues:** one per item, titled `CC-nnn: <title>`, with labels `type:*`, `severity:*` and `size:*`. The body has the description, wiki links and `Depends on`. Deliverables (`CC-nnn.n`) are sub-issues.
- **Views:** Board (by Status), Backlog by severity, Bugs.
- **New items:** see project `CLAUDE.md` §6. The next id is one more than the highest `CC-` issue title.

This page lists only work still to do and the recommended order. An item drops off it when it's merged; finished items stay on the board (Done) and in the issue history. It doesn't repeat any status or field. Change those on the board, not here.

**Release goal:** Android launch of Cathartic Colours.

## Phases and suggested order
1. **Core refactor:** [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8): game-code assembly definitions and PlayMode tests (the simulation's unit tests came with CC-006)
2. **Quick fixes, any time:** [CC-037](https://github.com/rtumelty/Cathartic-Colours/issues/64) (up-arrow exception) before [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42), which rewrites the same input code; [CC-028](https://github.com/rtumelty/Cathartic-Colours/issues/40) + [CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41) together (restart/UI); [CC-031](https://github.com/rtumelty/Cathartic-Colours/issues/43) before CC-013. The other small backlog items ([CC-019](https://github.com/rtumelty/Cathartic-Colours/issues/31), [CC-025](https://github.com/rtumelty/Cathartic-Colours/issues/37), [CC-026](https://github.com/rtumelty/Cathartic-Colours/issues/38), [CC-027](https://github.com/rtumelty/Cathartic-Colours/issues/39), [CC-030](https://github.com/rtumelty/Cathartic-Colours/issues/42), [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44), [CC-038](https://github.com/rtumelty/Cathartic-Colours/issues/65), [CC-040](https://github.com/rtumelty/Cathartic-Colours/issues/69)) sit in this phase on the board until they're scheduled.
3. **Gameplay:** [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19) → [CC-008](https://github.com/rtumelty/Cathartic-Colours/issues/20) → [CC-035](https://github.com/rtumelty/Cathartic-Colours/issues/55) (spawn rubber-banding, a spawn rule per mode on the CC-008 framework) → [CC-009](https://github.com/rtumelty/Cathartic-Colours/issues/21) → [CC-010](https://github.com/rtumelty/Cathartic-Colours/issues/22) / [CC-011](https://github.com/rtumelty/Cathartic-Colours/issues/23) / [CC-012](https://github.com/rtumelty/Cathartic-Colours/issues/24)
4. **Platform and monetisation:** [CC-039](https://github.com/rtumelty/Cathartic-Colours/issues/68) (version codes that never go backwards) in or before [CC-036](https://github.com/rtumelty/Cathartic-Colours/issues/57) (signing and Google Play), then CC-036 before [CC-015](https://github.com/rtumelty/Cathartic-Colours/issues/27) → [CC-016](https://github.com/rtumelty/Cathartic-Colours/issues/28), which need a store listing; [CC-013](https://github.com/rtumelty/Cathartic-Colours/issues/25) → [CC-014](https://github.com/rtumelty/Cathartic-Colours/issues/26)
5. **Polish:** [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29) (profile on device), [CC-018](https://github.com/rtumelty/Cathartic-Colours/issues/30) (can run in parallel any time; finalise after the new screens from 3–4 exist)

## Id to issue

Every open roadmap id and its issue. Rows are removed when an item is merged or closed. Deliverables are sub-issues of their item. Feature pages are linked from the issue body and from [index.md](index.md).

| Id | Issue | Title |
|---|---|---|
| CC-002 | [#8](https://github.com/rtumelty/Cathartic-Colours/issues/8) | Test infrastructure: game-code asmdefs + PlayMode tests |
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
| CC-025 | [#37](https://github.com/rtumelty/Cathartic-Colours/issues/37) | Missing guards on config, scene name and renderer teardown |
| CC-026 | [#38](https://github.com/rtumelty/Cathartic-Colours/issues/38) | Review mode rule gaps (Advanced dead ends, Color Merge size) |
| CC-027 | [#39](https://github.com/rtumelty/Cathartic-Colours/issues/39) | Runtime configuration leaks between menu, presets and resume |
| CC-028 | [#40](https://github.com/rtumelty/Cathartic-Colours/issues/40) | In-game UI wiring faults (status label, double restart) |
| CC-029 | [#41](https://github.com/rtumelty/Cathartic-Colours/issues/41) | Restart stacks duplicate grid cells; grid colour not applied |
| CC-030 | [#42](https://github.com/rtumelty/Cathartic-Colours/issues/42) | Input uses a hard-coded keyboard action instead of `GameControls` |
| CC-031 | [#43](https://github.com/rtumelty/Cathartic-Colours/issues/43) | Save file robustness (versioning, atomic write) |
| CC-032 | [#44](https://github.com/rtumelty/Cathartic-Colours/issues/44) | Presentation-layer cleanup (jelly material, unused scripts) |
| CC-035 | [#55](https://github.com/rtumelty/Cathartic-Colours/issues/55) | Rubber-band spawn colours against board imbalance |
| CC-036 | [#57](https://github.com/rtumelty/Cathartic-Colours/issues/57) | Store release: signing, keystore and Google Play publishing |
| CC-037 | [#64](https://github.com/rtumelty/Cathartic-Colours/issues/64) | Up arrow throws an Input System exception on every press |
| CC-038 | [#65](https://github.com/rtumelty/Cathartic-Colours/issues/65) | Save and load write a log line on every move |
| CC-039 | [#68](https://github.com/rtumelty/Cathartic-Colours/issues/68) | Android version codes go backwards between feature-branch and develop builds |
| CC-040 | [#69](https://github.com/rtumelty/Cathartic-Colours/issues/69) | Commit the settings files that Unity 6.6 re-saves on every build |
