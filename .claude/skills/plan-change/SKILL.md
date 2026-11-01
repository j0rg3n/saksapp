---
name: plan-change
description: Required workflow before any ad-hoc fix or change in SaksApp — update SPEC.md, create or update the GitHub issue, then implement and commit. Also covers milestones, labels and the public-repo rules.
---

# Planning a change

Work is tracked in GitHub issues and milestones on `j0rg3n/saksapp` (use `gh`). TODO.md is retired;
completed history lives in TODO_ARCHIVE.md, which is no longer added to.

## Order of work (no exceptions)

1. Update **SPEC.md** (behaviour/design).
2. Create or update the **GitHub issue** (task entry).
3. Implement (then run the `build-and-refresh` skill).
4. Commit locally, referencing the issue: `Fixes #N` when the commit completes it. Close the issue when done.
   Do not push unless explicitly asked.

## Issue and milestone conventions

- Milestones are the ordered groups of work, numbered in their title (`1. …`, `2. …`). Issues within a
  milestone can be done in parallel; note dependencies in the body ("Avhenger av #N").
- One issue per coherent unit of work, with a task checklist in the body. Keep issues lean — reference
  SPEC.md rather than duplicating it.
- Labels: `bug`, `enhancement`, `documentation`, `security`, `ops`, `performance`, `tech-debt`, `needs-refinement`.

## The repository is public

Issues, milestones, comments, commits, SPEC.md, code comments and tests are all public. Follow the
`no-real-data` skill: no real people, events, case content or production hostnames, ever.
