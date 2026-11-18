# CC-034 — Move roadmap tracking to GitHub Issues and Projects

| Field | Value |
|---|---|
| Type | Chore |
| Severity / impact | High |
| Complexity | M |
| Status | In progress |
| Branch | `feature/CC-034-github-issues` (from `develop`, created by the planning session on approval) |
| PR | — |
| Project board | [Cathartic Colours Roadmap](https://github.com/users/rtumelty/projects/1) (user project #1, private) |
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
- [x] `gh label list` shows all labels; `gh project field-list` shows Status (6 options), Severity, Size, Phase
- [ ] Views and workflows exist, created by the agent or by Ronan following the MANUAL steps (views created; Backlog sort and the two workflows are MANUAL steps 2–3)

**API check (2026-11-18).** GraphQL introspection of the live schema:
- Views: `createProjectV2View` and `updateProjectV2View` exist. They set name, layout, filter and visible fields (`configuration.visibleFieldIds` is the only configuration input). They can't set sort or group-by. A new board view groups by Status on its own.
- Workflows: the only workflow mutation is `deleteProjectV2Workflow`. `ProjectV2Workflow.enabled` can be read but not set, so enabling a workflow is a web-UI step.

**Result (2026-11-18).** Done by the Haiku worker, checked by the primary against the live API:
- Labels: `type:feature|bug|chore`, `severity:critical|high|medium|low`, `size:S|M|L|XL` (11). GitHub's default labels are kept.
- Project: user project **#1** "Cathartic Colours Roadmap", <https://github.com/users/rtumelty/projects/1>, linked to `rtumelty/Cathartic-Colours`, private (the default; see D4 note under Divergences).
- Fields: built-in `Status` options replaced with Backlog, Planning, Approved, In progress, In review, Done. New single-selects: `Severity` (Critical, High, Medium, Low), `Size` (S, M, L, XL), `Phase` (the six phases).
- Views: 1 "Backlog by severity" (table, filter `status:Backlog`), 2 "Board" (board, columns by Status), 3 "Bugs" (table, filter `label:type:bug`). Both tables show Title, Status, Labels, Sub-issues progress, Severity, Size, Phase (visible fields set by the primary at review).
- Workflows found: "Auto-add sub-issues to project" on; "Item closed", "Pull request merged", "Auto-close issue", "Item added to project" and "Pull request linked to issue" off.

### CC-034.2 — Import script for existing items
**Risk:** Low · **Worker:** Haiku · **Review:** primary, line by line before any live run

- `tools/roadmap-import.ps1` parses the summary table and detail sections of `roadmap.md` on the branch, **all items present at run time** (CC-001 to CC-034, not just CC-032)
- For each item: create the issue (title, labels, body with the description, wiki links and `Depends on` rewritten to issue numbers in a second pass), add it to the project, and set Status/Severity/Size/Phase. Done items (CC-001) are created then closed. Items with deliverables in the roadmap (CC-001, CC-006, and CC-034 itself) get sub-issues.
- **Idempotent:** an existing issue whose title starts `CC-nnn:` is updated, never duplicated
- **`-DryRun` by default:** prints every action. A live run needs `-Apply`.

**DoD (beyond template)**
- [x] Dry-run output reviewed by the primary and shown to Ronan
- [x] **Ronan approves the live run** (it posts about 34 public issues); a second live run produces no changes
- [x] Spot-check: CC-006 has 6 sub-issues and `Depends on` links to CC-033's issue

**Result (2026-11-18).** Live run approved by Ronan.
- **First `-Apply`:** created 47 issues (#3–#49): 34 items and 13 sub-issues. It closed 6 Done issues, added all 47 to the project, set 149 field values and linked 13 sub-issues. Pass 2 rewrote the bodies of CC-002, CC-006 and CC-033, whose dependencies were created after them. 0 errors.
- **Second `-Apply`:** created 0, updated 0, unchanged 47.
- **Spot-check:**
  - CC-006 is #12, labelled chore/high/L, with sub-issues #13–#18.
  - CC-006's `Depends on: #46, #45` points to CC-034 and CC-033.
  - CC-001 is #3, closed.
  - CC-034 is #46. Its PR will say `Closes #46`.
  - Board counts: Backlog 26, Approved 13, In progress 2, Done 6.
- **Not ours:** issue #2 "Test" and one project item with no content and no Status, both created by Ronan at 16:31 before the import. Left untouched.

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
2. **Sort the Backlog view by Severity** (the API can't set sort; CC-034.1). Open <https://github.com/users/rtumelty/projects/1/views/1> → click the ▾ beside the **Backlog by severity** tab → **Sort by** → **Severity** (ascending, so Critical comes first) → click **Save** on the tab (it shows a dot until saved).
3. **Enable the two workflows** (the API can't enable workflows; CC-034.1). Open the project → **⋯** (top right) → **Workflows**:
   - **Item closed** → **Edit** → *When* "Issue, Pull request" closed → *Set value* **Status: Done** → **Save and turn on workflow**.
   - **Pull request merged** → **Edit** → *Set value* **Status: Done** → **Save and turn on workflow**.
   Check the *Set value* dropdown in both: the Status options were replaced, so a stale default may show as empty.
   Leave the other workflows as they are.

## Decisions
All approved by Ronan 2026-11-18.
- **D1:** Id CC-034 and ADR-002, because CC-033 (Unity upgrade) and ADR-001 (pure simulation) were already taken when this was proposed.
- **D2:** Planned and done **before** CC-033 and CC-006, so those two run on the new tracking. The CC-006 plan stays approved-in-principle, and its approval steps follow the new workflow.
- **D3:** The import covers every item in `roadmap.md` at run time, including CC-033, CC-034 and the CC-006 deliverables.
- **D4:** The project is a user-level Project (owner rtumelty) linked to the repo. Issues are public, like the repo.

## Divergences
- **CC-034.1 (minor):** views were created through the API instead of by Ronan. The plan made the MANUAL route conditional on the API not supporting it. Only the Backlog sort and the two workflows remain MANUAL.
- **CC-034.1 (minor):** the primary set visible fields on both table views at review (one API call) instead of sending it back to the worker.
- **D4 note (open question):** D4 covers issue visibility (public) but not the board's. The project was left **private** (the default), so the board link in the wiki works only for Ronan. Make it public in project Settings → Visibility if it should match the issues.
- **CC-034.2 (minor, specifics):**
  - **Phase.** An item gets its own `##` heading if that's a phase. Otherwise it gets the first phase in "Suggested order" that mentions it. Items in neither (CC-019, CC-025, CC-026, CC-027, CC-030, CC-032) default to *Quick fixes*, with a warning. "Quick fixes, any time" is read as *Quick fixes*.
  - **Absorbed items.** CC-020 to CC-024 ("in CC-006") get a `Part of: #n` line, not a dependency or a sub-issue link. Sub-issues are only for deliverables.
  - **Links.** Relative wiki links in issue bodies are rewritten to `blob/develop/docs/wiki/…` URLs. CC-034's feature-page link resolves only once this branch is merged.
  - **Primary review fixes.** The worker's phase rule put CC-013 in *Quick fixes* (from "CC-031 before CC-013"), so the heading now comes first. Field-value paging was raised to 50. Live writes are paced at 1 s to stay under GitHub's secondary rate limit.

## Feature test plan
1. Open the project board: every CC item is in the right Status column; CC-001 is in Done (closed).
2. Open CC-006's issue: 6 sub-issues, labels high/L/chore, a working link to its feature page.
3. In a scratch test, create and close an issue: it moves to Done on its own. Delete the scratch issue afterwards.

## Status log
- 2026-11-18: Planned
- 2026-11-18: Approved by Ronan (plan, D1–D4, model routing)
- 2026-11-18: In progress (feature session started)
- 2026-11-18: CC-034.1 approved by Ronan; CC-034.2 started
- 2026-11-18: CC-034.2 live import approved by Ronan and run; rerun idempotent
