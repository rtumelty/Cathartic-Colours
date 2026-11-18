# CC-034 — Move roadmap tracking to GitHub Issues and Projects

| Field | Value |
|---|---|
| Type | Chore |
| Severity / impact | High |
| Complexity | M |
| Status | In progress |
| Branch | `feature/CC-034-github-issues` (from `develop`, created by the planning session on approval) |
| PR | — |
| Roadmap | [CC-034](../roadmap.md#cc-034) |
| Depends on | — (planned before CC-033 and CC-006) |
| Decision | ADR-002 (written in CC-034.3) |

## Goal
Item tracking (status, fields, deliverables, new finds) moves to GitHub Issues and a Project board. The wiki keeps the knowledge (feature pages, ADRs, system pages, test plan). Status changes stop needing commits, and parallel sessions stop conflicting on `roadmap.md`.

Direction agreed by Ronan in a side session on 2026-11-18. This page is the detailed plan for approval.

## Design
- **Issues:** one per roadmap item, titled `CC-nnn: <title>`. Labels `type:feature|bug|chore`, `severity:critical|high|medium|low`, `size:S|M|L|XL`. The body holds the description, wiki links (feature page, system pages, ADR) and `Depends on: #n, …`.
- **Project board** (user-level Project v2 linked to the repo):
  - Single-select `Status`: Backlog → Planning → Approved → In progress → In review → Done
  - Fields `Severity`, `Size`, `Phase` (Foundations, Core refactor, Quick fixes, Gameplay, Platform and monetisation, Polish)
  - Views: Board (by Status), Backlog by severity (table, Status = Backlog, sorted by Severity), Bugs (filter `type:bug`)
  - Workflows: item closed → Done; PR merged → Done
- **Deliverables** (`CC-nnn.n`) become sub-issues of the item, created by the planning session at approval, titled `CC-nnn.n: <title>`, with a `Risk` and `Worker` line in the body.
- **Ids:** the CC id stays in issue titles and branch names. The next id is one more than the highest `CC-` issue number, found with `gh issue list --search "CC- in:title" --state all`.
- **Wiki:** `roadmap.md` shrinks to a strategy page: release goal, phases, suggested order, a link to the board, and an id-to-issue table kept for the links. Feature pages link their issue in the header.

## Workflow changes (go into CLAUDE.md files in CC-034.3)
- **Approval (planning session):** set the issue to Approved, create the deliverable sub-issues, commit only wiki changes (feature page, ADRs) to the feature branch, and push. A roadmap-status commit to `develop` is needed only when `roadmap.md` strategy content changes.
- **Feature sessions:** move their issue and sub-issues through the statuses with `gh`. The PR body says `Closes #n`, so a merge sets Done. This replaces batched status commits.
- **New finds:** create an issue with `gh issue create` (title, labels, body, Phase), not a `roadmap.md` edit.
- **`gh` on Windows:** `C:\Program Files\GitHub CLI\gh.exe`; refresh PATH as for git. Already signed in as rtumelty with the `project` scope (checked 2026-11-18).

## Deliverables

### CC-034.1 — Labels, project, fields, views and automation
**Risk:** Low · **Worker:** Haiku · **Review:** primary

- Create the labels with `gh label create` (idempotent: `--force`)
- Create the project with `gh project create`, link it to the repo, create the fields with `gh project field-create`
- Views and built-in workflows: **check first** whether `gh`/the GraphQL API can create views and enable the "Item closed" / "Pull request merged" workflows. If they can't, write exact click paths as MANUAL (Ronan) steps rather than working around it.
- Record the project number and URL in project `CLAUDE.md` §6 (as part of .3) and in this page

**DoD (beyond template)**
- [ ] `gh label list` shows all labels; `gh project field-list` shows Status (6 options), Severity, Size, Phase
- [ ] Views and workflows exist, created by the agent or by Ronan following the MANUAL steps

### CC-034.2 — Import script for existing items
**Risk:** Low · **Worker:** Haiku · **Review:** primary, line by line before any live run

- `tools/roadmap-import.ps1` parses the summary table and detail sections of `roadmap.md` on the branch, **all items present at run time** (CC-001 to CC-034, not just CC-032)
- For each item: create the issue (title, labels, body with the description, wiki links and `Depends on` rewritten to issue numbers in a second pass), add it to the project, and set Status/Severity/Size/Phase. Done items (CC-001) are created then closed. Items with deliverables in the roadmap (CC-001, CC-006, and CC-034 itself) get sub-issues.
- **Idempotent:** an existing issue whose title starts `CC-nnn:` is updated, never duplicated
- **`-DryRun` by default:** prints every action. A live run needs `-Apply`.

**DoD (beyond template)**
- [ ] Dry-run output reviewed by the primary and shown to Ronan
- [ ] **Ronan approves the live run** (it posts about 34 public issues); a second live run produces no changes
- [ ] Spot-check: CC-006 has 6 sub-issues and `Depends on` links to CC-033's issue

### CC-034.3 — Wiki and workflow switch-over
**Risk:** Medium · **Worker:** primary · **Review:** primary

- `roadmap.md` → strategy page (release goal, phases, suggested order, board link, id-to-issue table); item details now live in the issues
- Feature pages (CC-001, CC-006, CC-034) link their issue in the header
- `decisions/ADR-002-issue-tracking.md` (context, options considered: wiki only, Issues only, hybrid; decision: hybrid)
- User-level `~/.claude/CLAUDE.md`: §2.1 hand-off, §4 exception narrowed (status no longer committed), §5 "Roadmap items" (fields live on the board; the id rule; deliverables as sub-issues). The user-level file isn't in git: change it locally and note it in the log.
- Project `CLAUDE.md` §6: board URL, `gh` path, the new-finds rule
- Index, log (`schema`); memory note on batched status commits retired (planning-session housekeeping)

**DoD (beyond template)**
- [ ] No status or field information left only in the wiki; every CC id links to its issue
- [ ] Both CLAUDE.md files describe the same workflow; lint pass clean

## Tests
No game code, so no Unity tests. Verification:
- The script's dry run against the current `roadmap.md` (expected item count, labels and fields per item)
- Rerun idempotency: a second `-Apply` reports 0 created
- A link check across the wiki after the shrink

## Risk and model routing (approved)
| Deliverable | Risk | Worker | Notes |
|---|---|---|---|
| CC-034.1 | Low | Haiku | Mechanical `gh` setup |
| CC-034.2 | Low | Haiku | Outward-facing bulk action: dry run by default, primary reviews, Ronan approves the live run |
| CC-034.3 | Medium | Primary | Changes the workflow rules for every session |

## MANUAL (Ronan)
1. **Approve the live import** after reviewing the dry-run output (CC-034.2).
2. **Only if the API can't do it** (decided in CC-034.1): create the three views and enable the two built-in workflows in the project's web UI, following the click paths the session writes.

## Decisions
All approved by Ronan 2026-11-18.
- **D1:** Id CC-034 and ADR-002, because CC-033 (Unity upgrade) and ADR-001 (pure simulation) were already taken when this was proposed.
- **D2:** Planned and done **before** CC-033 and CC-006, so those two run on the new tracking. The CC-006 plan stays approved-in-principle, and its approval steps follow the new workflow.
- **D3:** The import covers every item in `roadmap.md` at run time, including CC-033, CC-034 and the CC-006 deliverables.
- **D4:** The project is a user-level Project (owner rtumelty) linked to the repo. Issues are public, like the repo.

## Divergences
_None yet._

## Feature test plan
1. Open the project board: every CC item is in the right Status column; CC-001 is in Done (closed).
2. Open CC-006's issue: 6 sub-issues, labels high/L/chore, a working link to its feature page.
3. In a scratch test, create and close an issue: it moves to Done on its own. Delete the scratch issue afterwards.

## Status log
- 2026-11-18: Planned
- 2026-11-18: Approved by Ronan (plan, D1–D4, model routing)
- 2026-11-18: In progress (feature session started)
