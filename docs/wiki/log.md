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
## [2026-11-18] plan | roadmap.md | CC-006 scope: all simulation systems Burst-compiled; CC-008 scope: swappable spawn/end/scoring rules with a Burst-compatible mechanism (Ronan, from CC-001 quiz)
## [2026-11-18] schema | CLAUDE.md | §3: favour Burst-compatible solutions in ECS code; note that IMergeUtility blocks Burst
## [2026-11-18] implement | CC-001 | PR #1 merged; CC-001 → Done (status batched with CC-006 planning)
## [2026-11-18] plan | roadmap.md | CC-033 added (Unity 6000.6.9f1 upgrade, blocks CC-006); CC-002 rescoped to game-code asmdefs + PlayMode (simulation part moves to CC-006); suggested order updated
## [2026-11-18] plan | CC-006 | Feature page and ADR-001 (Proposed) written: 6 deliverables, absorbs CC-020–CC-024; awaiting approval
## [2026-11-18] plan | CC-034 | GitHub Issues + Projects migration planned from a side-session proposal (id CC-034, ADR-002, since CC-033 and ADR-001 were taken); ordered before CC-033 and CC-006; awaiting approval
## [2026-11-18] approve | CC-034 | Plan, D1–D4 and model routing approved by Ronan
## [2026-11-18] approve | CC-006 | Plan approved; D3 changed: struct rule sets renamed *MergeRules behind IMergeRules (I prefix only on the interface); order CC-034 → CC-033 → CC-006; ADR-001 Accepted
## [2026-11-18] lint | .gitignore | Obsidian ignore rules didn't match docs/.obsidian; fixed to **/.obsidian and docs/.obsidian/workspace.json untracked
## [2026-11-18] implement | develop | 940c674 (Burst guidance, pushed to the CC-001 branch after PR #1 was squash-merged) brought into develop
## [2026-11-18] implement | CC-034 | Status → In progress; feature session started
## [2026-11-18] implement | CC-034.1 | Labels (11), user project #1 with Status/Severity/Size/Phase fields, three views created by API; API cannot set view sort or enable workflows, so Backlog sort and Item closed / PR merged workflows are MANUAL (Ronan)
## [2026-11-18] diverge | CC-034.1 | Views created by agent, not Ronan (API supports it); primary set table visible fields at review; board visibility (private) raised as open question on D4
## [2026-11-18] approve | CC-034.1 | Approved by Ronan; CC-034.2 started
## [2026-11-18] implement | CC-034.2 | tools/roadmap-import.ps1 (dry run by default, -Apply, -Ids, -ShowBodies); primary review fixed phase rule (CC-013), paging and write pacing; dry run: 34 items, 13 sub-issues, 47 creates, 6 closes, 0 errors; live run awaiting Ronan
## [2026-11-18] diverge | CC-034.2 | Minor specifics: phase from heading then suggested order (default Quick fixes); absorbed items get "Part of"; wiki links made absolute
## [2026-11-18] implement | CC-034.2 | Live import (approved by Ronan): 47 issues #3–#49 (34 items, 13 sub-issues), 6 closed, all on project #1; rerun 0 changes; CC-006 = #12 (subs #13–#18), CC-034 = #46
## [2026-11-18] approve | CC-034.2 | Approved by Ronan; #48 closed; CC-034.3 (#49) started
## [2026-11-18] decision | ADR-002 | Hybrid tracking accepted: GitHub issues, sub-issues and project #1 hold status and fields; the wiki keeps knowledge
## [2026-11-18] implement | CC-034.3 | roadmap.md shrunk to strategy page + id-to-issue table; 83 roadmap.md#cc-nnn links → issue URLs; feature headers link issues (status/fields removed); index statuses removed; features/systems READMEs, ADR template, test plan updated
## [2026-11-18] schema | ~/.claude/CLAUDE.md | (not in git) §1.5, §2.1 approval via gh + sub-issues, feature sessions move statuses, Closes #n; §2.2 sub-issue statuses; §3 DoD; §4 status never committed; §5 tree, Roadmap items on GitHub (id rule, sub-issues, new finds, field table), lint checks; duplicate §2.1 bullet removed
## [2026-11-18] schema | CLAUDE.md | §6: planning hand-off via gh, tracking (board #1, fields, labels, views), new-finds commands, status changes via gh, gh on Windows
## [2026-11-18] lint | docs/wiki | 139 relative links, 0 broken by CC-034.3; 2 already broken (index → CC-006 page and ADR-001, which exist only on feature/CC-006-pure-simulation); no status values left in wiki pages; 34/34 ids in the id-to-issue table
## [2026-11-18] diverge | CC-034.3 | CC-006 feature page is not on this branch, so its issue header and link rewrite are left to the planning or CC-006 session (raised with Ronan); feature headers drop status/fields
## [2026-11-18] approve | CC-034.3 | Approved by Ronan; #49 closed; MANUAL steps 2–3 done by Ronan (workflows verified enabled); CC-006 page fix assigned to the planning session
## [2026-11-18] implement | CC-034 | Feature test cases folded into test-plan.md (new area TRACK, 4 cases); #46 → In review; PR to develop opened
## [2026-11-18] lint | CC-006 | Feature page header links issue #12 (status/fields removed), deliverables link sub-issues #13–#18, roadmap.md#cc-nnn links → issue URLs (also ADR-001); Burst-alignment divergence marked approved
## [2026-11-18] implement | CC-034 | PR #50 merged; squash 3096558 matches the branch tip; #46 closed by hand, since "Closes #n" only fires on merges into the default branch (master)
## [2026-11-18] approve | CC-006 | Burst alignment of plan and ADR-001 §5 approved by Ronan
## [2026-11-18] plan | CC-033 | Feature page written: 3 deliverables (strip packages + Android module + editor upgrade; FMOD 2.04 + API fixes; test-runner smoke run + docs), routing Sonnet/primary/Haiku
## [2026-11-18] approve | CC-033 | Plan, D1–D4 and routing approved by Ronan; #45 Approved, sub-issues #51–#53 created
## [2026-11-18] implement | CC-033.1 | Packages stripped (manifest 60 → 17, evidence on the feature page); project upgraded to 6000.6.9f1 in batchmode; resolution clean; URP 17.6 settings migration reviewed; FMOD 2.03 CS0619 left for CC-033.2
## [2026-11-18] diverge | CC-033.1 | Hub CLI headless install gone: Unity CLI installed, elevation refused, Android module via Hub GUI (Ronan); Visual Studio integration removed (Ronan); ugui/physics2d/terrain removed on the 6.6 dependency graph
## [2026-11-18] decision | ADR-003 | FMOD for Unity 2.04 as an embedded UPM package in Packages/com.fmod.fmod-for-unity.v204 (Ronan); settings in Assets/Resources/FMOD
## [2026-11-18] implement | CC-033.2 | FMOD 2.04 embedded, 2.03 Assets/Plugins removed, settings moved with GUIDs intact; batchmode compile 0 errors; no game-code API changes needed; Android module installed by Ronan via Hub
## [2026-11-18] diverge | CC-033.2 | UPM tarball instead of .unitypackage (embedded package, ADR-003); agent imported instead of MANUAL step 1; started before CC-033.1 approval at Ronan's request
## [2026-11-19] implement | CC-033 | Merged develop (saved FMOD Studio project, Mobile banks, Android build profiles, Unity IAP 5.5.1) into the feature branch; FMOD settings now migrated to 2.04; event cache untracked again
## [2026-11-19] implement | CC-033 | 57 .meta files below Unity 6.6's minimum format re-saved (56 in the FMOD package, MainMenu.uxml.meta); plugin platform flags unchanged; local package change recorded in systems/audio-fmod
## [2026-11-19] implement | CC-033.3 | EditMode and PlayMode -runTests on 6000.6.9f1 both exit 0 with a results XML (0 tests); ProjectSettings re-saved in the 6.6 format; test plan area TOOL added (TP-TOOL-01..03)
## [2026-11-19] schema | CLAUDE.md | §1 Unity 6000.6.9f1, Entities 6.6.0, URP 17.6.0, Unity IAP, FMOD for Unity 2.04; §4 editor path and close-the-editor note; §5 FMOD Studio 2.04 migration note replaces the stale .unsaved note
## [2026-11-19] diverge | CC-033.3 | Primary wrote the docs, Haiku ran the tests; corrected the merge note: Unity 6.6 writes a trailing i on integer hex arrays
## [2026-11-18] schema | ~/.claude/CLAUDE.md | (not in git) §4: commit messages and PRs must be readable without knowledge of the project or its plans; ids lead the subject but are never the only explanation
## [2026-11-19] plan | CC-003, CC-004 | Rescoped after Ronan's develop commits (FMOD project saved, Mobile banks, Android build profiles); CC-033 keeps com.unity.purchasing (Ronan)
## [2026-11-19] approve | CC-033 | PR #54 squash-merged into develop as 73ee1cc, identical to the branch tip; #45 and sub-issues #51-#53 Done
## [2026-11-19] plan | roadmap | Review after CC-033: Foundations continues with CC-003, CC-004, CC-005; CC-006 is unblocked (its branch needs develop merged in); audio-fmod hazard marked resolved pending CC-003
## [2026-11-19] plan | CC-035 | New backlog item: spawn colour rubber-banding against board colour imbalance (Ronan); depends on CC-006
## [2026-11-19] schema | CLAUDE.md | §6 two working copies: Cathartic-Colours is the agent sandbox, Cathartic-Colours_2 is Ronan's manual copy; agents ask before switching the manual copy's branch; §4 batchmode note updated
## [2026-11-19] schema | CLAUDE.md | Two-working-copies rule moved out of the project CLAUDE.md into an uncommitted CLAUDE.local.md, since it describes one machine's setup (Ronan); §4 batchmode note restored
## [2026-11-19] implement | CC-006.1 | CatharticColours.Simulation assembly (block, size, tier and mode enums, namespace Simulation) and EditMode test assembly; 4 smoke tests pass from the CLI; assets unchanged; TestResults/ gitignored
## [2026-11-19] schema | CLAUDE.md | §3 Simulation namespace and the ADR-001 pattern (rules in the pure simulation, ECS wraps it); §4 test-run and Assembly-CSharp notes updated for the Simulation tests
## [2026-11-19] approve | CC-006.1 | Approved by Ronan; #13 Done
## [2026-11-19] implement | CC-006.2 | Pure board model (Block, BoardState, SimEvent, StepResult), struct rule sets behind IMergeRules with MergeRulesFactory, BoardSimulation.Step with parity incl. CC-020/CC-021; 200 EditMode tests pass, Burst compile checked
## [2026-11-19] diverge | CC-006.2 | Old IMergeUtility classes removed as planned, so BlockMovementSystem and GameEndConditionSystem now call MergeRulesFactory (name clash on MergeResult); restored-indicator MovingBlockTag slide noted for .3
## [2026-11-19] schema | CLAUDE.md | §3 rules-as-strategies now IMergeRules structs and MergeRulesFactory; Simulation.Block name-clash note
## [2026-11-19] approve | CC-006.2 | Approved by Ronan, including the old-systems adapter divergence; #14 Done
## [2026-11-19] implement | CC-006.3 | BoardStepSystem (Burst) wraps BoardSimulation.Step and replaces five move systems; MergeEffectsSystem for particles; wobble from GameRenderer; ScoringConfigComponent; CC-023 fixed; CC-024 dead code removed; OccupantTracking keeps entity identity; 208 EditMode tests pass, ECS world check passed
## [2026-11-19] diverge | CC-006.3 | Resume no longer replays resolve/spawn (part of CC-022, save fix stays in .5); CC-025 fixed as a side effect; SpawnColorSystemTag also removed; ClearMoveDirectionSystem made Burst
## [2026-11-19] implement | wiki | ecs-frame-order rewritten; merge-rules, presentation, audio-fmod, configuration, input, save-system, ui-layout updated for BoardStepSystem
## [2026-11-19] approve | CC-006.3 | Manual regression pass done by Ronan; approved including the CC-022/CC-025 side effects; #15 Done
## [2026-11-19] implement | CC-006.4 | Colour roll advances the random state (CC-020); spawn picks uniformly from empty cells with one draw, fails only on a full board (CC-021); BoardStepSystem saves such moves; Fixes_ tests replace Pins_; 207 EditMode tests pass
## [2026-11-19] approve | CC-006.4 | Approved by Ronan; #16 Done
## [2026-11-19] implement | CC-006.5 | Save records the idle state after a move; restore always waits for input (older saves too); no format change; TP-SAVE-03 updated, TP-SAVE-07 added; save-system and ecs-frame-order CC-022 items removed
## [2026-11-19] approve | CC-006.5 | Approved by Ronan (TP-SAVE cases pass); #17 Done. ObjectDisposedException in GameRenderer.CleanUp during teardown is pre-existing: added to CC-025 (#37)
## [2026-11-19] implement | CC-006.6 | Test plan cases for CC-021/022/023 updated, TP-OVER-07 added, Regression passes section; #32-#36 closed; CC-002 description confirmed
## [2026-11-19] lint | CC-006 pages | Stale lines fixed in merge-rules, configuration, presentation; ADR-001 Burst set noted; roadmap.md strikethrough status reported to the planning session
## [2026-11-19] approve | CC-006.6 | Approved by Ronan; #18 Done; PR #56 opened to develop, #12 In review
## [2026-11-19] approve | CC-006 | PR #56 squash-merged into develop as 2a4177d, identical to the branch tip; #12, sub-issues #13-#18 and absorbed CC-020 to CC-024 (#32-#36) Done
## [2026-11-19] plan | roadmap | Review after CC-006: CC-025 (#37) narrowed (singleton guard done in CC-006; teardown ObjectDisposedException added); strikethrough removed from the phase order, since status lives on the board
## [2026-11-19] schema | CLAUDE.md, roadmap | roadmap.md holds open work only (Ronan): merged items removed from the phase order and id-to-issue table (CC-001, CC-006, CC-020 to CC-024, CC-033, CC-034); CC-035 placed after CC-008
## [2026-11-19] implement | CC-005 | points_system had no commits outside master (tip bf5152d); remote branch deleted with Ronan's approval, done in the planning session; #11 closed
## [2026-11-19] plan | CC-036 | New backlog item: release signing, keystore and Google Play publishing, split out of CC-004 because the game isn't going to the store yet (Ronan)
## [2026-11-19] implement | CC-003 | Ronan re-saved the FMOD Studio project in 2.04 (96298b1): Mobile platform and encoding now saved in Metadata, .unsaved removed from git; banks rebuilt in 2.04 (576f6cb)
## [2026-11-19] approve | CC-003 | Plan, D1–D3 and routing approved by Ronan; #9 Approved, sub-issues #58–#60; Ronan's 2.04 re-save (96298b1) replaced the MANUAL Save step
## [2026-11-19] implement | CC-003.1 | FMOD Studio per-machine folders .cache/ (233 files) and .user/ (3 files) untracked, kept on disk; .gitignore added for .cache/, .user/, .unsaved/; saved-project checks passed (Desktop + Mobile platforms, 2 encodings, 5 events)
## [2026-11-19] approve | CC-003.1 | Approved by Ronan; #58 Done
## [2026-11-19] implement | CC-003.2 | tools/build-fmod-banks.ps1 wraps fmodstudiocl -build; -diagnostic clean (no errors or warnings); a clean-checkout build rewrote all four banks byte-identical to the committed ones
## [2026-11-19] implement | CC-003 MANUAL 1 | Ronan: manual copy on 442e23b, FMOD Studio 2.04 reopened the project and the editor played all sounds; the pull removed the copy's tracked .user/ files, so its layout was restored from a backup as ignored files
## [2026-11-19] approve | CC-003.2 | Approved by Ronan after the listening check; #59 Done
## [2026-11-19] implement | CC-003.3 | Audio docs updated for the saved 2.04 project: audio-fmod.md (layout, platforms and encodings, rebuilding the banks, Unity platform-to-Build-folder findings; .unsaved hazard section removed), test cases TP-AUDIO-05 rewritten, TP-AUDIO-06 and TP-TOOL-04 added, ADR-003 note, index
## [2026-11-19] schema | CLAUDE.md | §1 FMOD Studio 2.04 with Desktop and Mobile banks committed; §5 FMOD bullet: save, close Studio, rebuild with tools\build-fmod-banks.ps1, commit Build/ with Metadata/; removed the migrate-to-2.04 text
