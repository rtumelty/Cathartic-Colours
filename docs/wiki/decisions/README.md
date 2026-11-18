# Architecture decision records (ADRs)

An ADR records one significant decision: the context, the options considered, what was chosen, and the consequences.

**Filename:** `ADR-<nnn>-<slug>.md`, for example `ADR-001-<slug>.md`. Numbering starts at **ADR-001**. `ADR-000` is the template, not a decision. Numbers are never reused.

**Template:** [ADR-000-template.md](ADR-000-template.md). Copy it and fill in every section.

## When an ADR is needed

Write one whenever a change introduces a new code pattern, or changes an existing one (user-level `CLAUDE.md` §1, step 2). Examples:

- a new way of structuring ECS systems, components or event entities
- a new strategy, factory or configuration approach
- a new persistence, audio or input mechanism
- a choice between libraries or platform services

Following an existing pattern does not need an ADR. Documentation-format choices (such as this page) do not need one either.

## Statuses

| Status | Meaning |
|---|---|
| Proposed | Drafted in a feature plan, awaiting Ronan's approval |
| Accepted | Approved and in force |
| Superseded by ADR-<nnn> | Replaced by a later ADR; keep the file, link to the replacement |
| Deprecated | No longer applies and has no replacement |

## Index

ADRs are listed in [index.md](../index.md) as they are added.
