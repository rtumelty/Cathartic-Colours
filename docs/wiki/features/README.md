# Feature pages

One page per roadmap item that has a plan. The planning session writes the page when a plan is drafted, and commits it to the feature branch on approval (user-level `CLAUDE.md` §2.1).

**Filename:** `CC-<nnn>-<slug>.md`, for example `CC-001-wiki-scaffold.md`. The id matches the roadmap; the slug is lowercase, hyphenated.

**Example:** [CC-001 Wiki scaffold and system pages](CC-001-wiki-scaffold.md).

## Sections

Use these headings in this order.

1. **Header table:** Type, Severity / impact, Complexity, Status, Branch (`feature/CC-<nnn>-<slug>`), PR, Roadmap link (`[CC-<nnn>](../roadmap.md#cc-<nnn>)`).
2. **Goal:** one or two sentences on the outcome.
3. **Scope:** In and Out. Anything found out of scope becomes a backlog item, not part of this feature.
4. **Approach:** the chosen design, and the existing codebase patterns it follows.
5. **Deliverables:** one `### CC-<nnn>.<n> — <title>` block per deliverable, each with:
   - **Risk:** Low / Medium / High
   - **Worker:** Haiku / Sonnet / primary model
   - **Review:** who reviews the output before commit
   - **Definition of Done:** checklist (start from the template in user-level `CLAUDE.md` §3)
6. **Tests:** unit and system tests added, or the reason none apply.
7. **Risk and model routing:** table of deliverable, risk, worker, notes. Ronan approves this with the plan.
8. **MANUAL (Ronan):** numbered steps with exact click paths, for editor, FMOD, store or console work.
9. **Decisions:** numbered `D1`, `D2`… for choices made in the feature. Anything architectural also gets an ADR in [decisions](../decisions/README.md).
10. **Divergences:** dated entries for any change from the approved plan. Scope, architecture, deliverable order or model routing changes need Ronan's approval first.
11. **Feature test plan:** the shortest path for a tester to check the feature. Cases are folded into [test-plan.md](../test-plan.md) at completion.
12. **Status log:** dated one-line entries (Planned, Approved, In progress, In review, Done).

## Conventions

- Keep pages short. Link to system pages rather than repeating them.
- Every behavioural claim about existing code cites its source file (`Assets/Scripts/...`).
- Status in the header must match the roadmap. Update both in the same commit.
