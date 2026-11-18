# Wiki log — Cathartic Colours

Append-only. Entry prefix: `## [YYYY-MM-DD] <op> | <id or page> | <summary>`

## [2026-11-18] schema | CLAUDE.md | Workflow split into user-level ~/.claude/CLAUDE.md and project CLAUDE.md
## [2026-11-18] plan | roadmap.md | Roadmap created with CC-001–CC-019 from initial project scan and planning discussion
## [2026-11-18] plan | index.md | Minimal index created (full scaffold under CC-001)
## [2026-11-18] plan | CC-001 | Feature page written: 4 deliverables, routing Haiku/Sonnet/Sonnet/Sonnet+Haiku; awaiting approval
## [2026-11-18] approve | CC-001 | Plan, decisions D1–D3 and model routing approved by Ronan
## [2026-11-18] schema | CLAUDE.md | Hand-off workflow: planning session commits roadmap to develop, creates and pushes the feature branch with the plan, kickoff names the branch; added §6 Sessions and environment
## [2026-11-18] schema | CLAUDE.md | §6: feature sessions run locally only with Remote Control; no cloud sessions
## [2026-11-18] diverge | CC-001 | Branch is feature/CC-001-wiki-setup, not feature/CC-001-wiki-scaffold as planned; references corrected (minor)
## [2026-11-18] implement | CC-001 | Status → In progress; feature session started
## [2026-11-18] implement | CC-001.1 | Wiki structure: features/decisions/systems READMEs, ADR-000 template, docs/raw register, test-plan skeleton (8 areas), index recategorised; 0 broken links
## [2026-11-18] implement | CC-001.2 | System pages ecs-frame-order, merge-rules, configuration; frame order verified against attributes by primary
## [2026-11-18] plan | roadmap.md | Backlog CC-020–CC-027 added from CC-001.2 findings (RNG, spawn, resume, audio, dead code, guards, mode rules, config)
## [2026-11-18] implement | CC-001.3 | System pages save-system, audio-fmod, input, ui-layout, presentation; configuration.md grid-colour claim corrected
## [2026-11-18] plan | roadmap.md | Backlog CC-028–CC-032 added from CC-001.3 findings (UI wiring, restart grid leak, input asset, save robustness, presentation cleanup); CC-003, CC-025 extended
## [2026-11-18] schema | CLAUDE.md | §2 system-page list matches the eight pages in docs/wiki/systems/ (CC-001 D3)
## [2026-11-18] implement | CC-001.4 | test-plan.md seeded with 50 current-behaviour cases (8 areas); system pages' Related test cases filled; configuration.md restart claim corrected (no header restart button)
## [2026-11-18] lint | docs/wiki | Findings: 5 stale "planned page / plain text" claims (systems README, configuration, ecs-frame-order, merge-rules) → replaced with links; CC-030 summary title differed from detail → aligned; TP-ACM-07 roadmap id → CC-032; 2 placeholder links in ADR template / features README → kept as code spans. Accepted: TP-OVER-04 keeps "—" (notes CC-028/029 but isn't expected to fail); D3 wording left as approved. Link check: 0 broken relative links or anchors outside code spans; 0 orphans; statuses consistent
## [2026-11-18] plan | roadmap.md | CC-032 extended: Advanced Color Merge top-tier clear emits transparent particles
## [2026-11-18] implement | CC-001.4 | test-plan.md: TP-OVER-05 added (editor-only grid cell count after restart, CC-029); old TP-OVER-05 renumbered TP-OVER-06; references updated
## [2026-11-18] decision | CC-001 D3 | Wording clarified at Ronan's request: two new system pages (configuration, presentation) plus scoring/spawn/end folded into merge-rules; decision unchanged
## [2026-11-18] approve | CC-001 | CC-001.4 approved; all deliverables Done; CC-001 → In review, PR feature/CC-001-wiki-setup → develop
