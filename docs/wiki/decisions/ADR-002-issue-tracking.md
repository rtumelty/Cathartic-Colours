# ADR-002 — Track roadmap items in GitHub Issues and a Project board

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-18 |
| Roadmap id | [CC-034](https://github.com/rtumelty/Cathartic-Colours/issues/46) |
| Supersedes | — |

## Context
Until CC-034, `docs/wiki/roadmap.md` held every roadmap item: its fields (type, severity, complexity, status, dependencies), its description and its deliverable table. That caused three problems:

- **Status commits.** Every status change was a commit. Feature sessions changed status on their branch and the planning session changed it on `develop`, so there were batched "status-only" updates and a memory note on how to batch them.
- **Merge conflicts.** Parallel sessions (planning, feature, side sessions) all edited the same summary table in `roadmap.md`.
- **No queries.** Questions like "every open bug by severity" or "what's in progress" meant reading the whole file.

The wiki model (user-level `CLAUDE.md` §5) still needs a durable home for knowledge: plans, decisions, how systems work, and the test plan.

## Options

### Option A — Wiki only (status quo)
- **Description:** keep items, fields and status in `roadmap.md`.
- **Pros:** one place; works offline; everything is in git history.
- **Cons:** status commits and merge conflicts remain; no board or filtering; sessions have to commit just to say "in progress".

### Option B — Issues only
- **Description:** move everything, plans and decisions included, into GitHub issues and drop the wiki's planning pages.
- **Pros:** one tool; nothing to keep in sync.
- **Cons:** loses the wiki's reviewed, versioned knowledge: feature plans approved on a branch, ADRs, system pages and lint passes. Issue text isn't reviewed in PRs. Agents lose the local, greppable context they plan from.

### Option C — Hybrid
- **Description:** GitHub holds **tracking**: one issue per item (`CC-nnn: <title>`), labels for type, severity and size, deliverables as sub-issues, and a Project board with Status, Severity, Size and Phase. The wiki keeps **knowledge**: feature pages, ADRs, system pages and the test plan. `roadmap.md` becomes a strategy page with an id-to-issue table.
- **Pros:** status changes are `gh` calls, not commits; no shared table to conflict on; the board gives views and filters; `Closes #n` in a PR and the project workflows move items to Done without anyone committing; the knowledge stays in git and is reviewed in PRs.
- **Cons:** two places to look; a GitHub dependency (sessions need `gh` with the `project` scope); views and workflows partly need the web UI; issue bodies and wiki pages can drift unless the rule below is kept.

## Decision
Option C, hybrid. It removes the status commits and conflicts, and it keeps the reviewed knowledge in the wiki that every session plans from.

Rules that make it work:
- **Single source per fact.** Status and fields live only on the board and issue. The wiki never repeats them; feature pages link their issue instead.
- **Ids stay.** The `CC-nnn` id stays in issue titles, branch names and wiki links. The next id is one more than the highest `CC-` issue title. Ids are never reused.
- **Deliverables are sub-issues,** created by the planning session at approval, with `Risk` and `Worker` in the body.
- **New finds become issues** (`gh issue create`), not `roadmap.md` edits.

## Consequences
- The planning session's approval step is now: set the issue to Approved, create the sub-issues, and commit only wiki knowledge (the feature page and any ADRs) to the feature branch. A `develop` commit is needed only when the strategy in `roadmap.md` changes.
- Feature sessions move their own issue and sub-issues through the statuses with `gh`. The PR says `Closes #n`, and a merge sets Done through the board's "Item closed" and "Pull request merged" workflows.
- `log.md` and `index.md` are still shared files. Two branches that both append to them can still conflict, but the conflict is trivial: keep both sides.
- The `roadmap.md` anchors (`#cc-nnn`) are gone. Wiki links to an item point at its issue URL. `log.md` keeps its old entries unchanged (append-only).
- `tools/roadmap-import.ps1` did the one-time import (CC-034.2). It reads the old `roadmap.md` format, so on the new page it finds no items and does nothing. It's kept for history and as an example of the `gh`/GraphQL calls.
- Project setup (board #1, fields, views, the API limits on views and workflows) is recorded on the [CC-034 feature page](../features/CC-034-github-issues.md#cc-0341--labels-project-fields-views-and-automation). The workflow rules are in the user-level and project `CLAUDE.md` files.

## Status
Accepted, 2026-11-18 (CC-034 plan approved by Ronan).
