# CLAUDE.md - AI Assistant Guidelines

## Building the Project

```bash
# Build the Docker container
docker compose build

# Run the application
docker compose up
```

## Code Quality

- Build is required to verify code compiles (no local dotnet available)
- After any code changes, always run `docker compose build` to verify compilation
- **Tests and sample data must never contain real case content** — no real names, addresses,
  companies, incidents, case text or attachment file names, even when reproducing a reported bug.
  Use neutral placeholders (`Person A`, `Eksempelsak`, `vedlegg-a.pdf`). The same applies to code
  comments, SPEC.md and GitHub issues.
- After verifying the build succeeds, refresh the running container:
  ```bash
  docker compose up -d --force-recreate
  ```

## Database

```bash
# Apply migrations (run inside container)
dotnet ef database update
```

## Project Structure

- `SaksAppWeb/` - Main ASP.NET Core application
- Uses Entity Framework Core with SQLite
- PDF generation via PdfSharpCore

## Git Workflow

- Make local commits for major changes
- Do not push to remote unless explicitly requested

## Planning

Work is tracked in GitHub issues and milestones on `j0rg3n/saksapp` (use `gh`). TODO.md is retired;
completed history lives in TODO_ARCHIVE.md, which is no longer added to.

- **The repository is public.** Issues, milestones and comments are public too — never put real
  case content, names, addresses or production hostnames in them (see Code Quality).
- Milestones are the ordered groups of work, numbered in their title (`1. …`, `2. …`) to fix
  the order. Issues within a milestone can be done in parallel; note dependencies in the body
  ("Avhenger av #N").
- One issue per coherent unit of work, with a task checklist in the body. Keep issues lean —
  reference SPEC.md rather than duplicating it.
- Labels: `bug`, `enhancement`, `documentation`, `security`, `ops`, `performance`, `tech-debt`,
  `needs-refinement`.
- **Before any ad-hoc fix or change**: update SPEC.md (behaviour/design) and create or update the
  GitHub issue (task entry) first, then implement. No exceptions.
- Reference the issue in the commit (`Fixes #N` when the commit completes it) and close it when
  the change is done.

## Pre-deployment review

Before deploying at the end of a milestone, review the changes going out across all of:

- **Security** — authn/authz gaps, injection, secrets or tokens at rest, CSRF, IDOR, data exposure
- **Reliability** — error handling, retries, partial failure, migration and startup safety, backup/restore paths
- **Stability** — regressions, breaking schema or API changes, resource exhaustion (disk, memory, handles)
- **Responsiveness** — UI latency, blocking calls on request paths, phone and desktop layouts
- **Performance** — N+1 queries, unbounded result sets, work done in memory that belongs in the query
- **Code smells** — duplication, drift between related call sites, dead code, tests that pass for the wrong reason

Record anything found in SPEC.md and as GitHub issues (in the milestone being deployed) before
fixing it, per the Planning rule above.
Deploy only once the findings are either fixed or explicitly accepted.

`/security-review` and `/code-review` cover parts of this; neither is a substitute for the whole list.

## Daily routines

- **todoist-triage**: `/todoist-triage styret/saksapp` — run daily to pull new tasks from Todoist into this repo.
  Tasks become GitHub issues (per the Planning rules), not TODO.md entries.
