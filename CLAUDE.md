# CLAUDE.md - AI Assistant Guidelines

## Project skills

Task workflows live in `.claude/skills/`:

- `build-and-refresh` — build/test in Docker and refresh the container (required after any code change)
- `plan-change` — SPEC.md + GitHub issue first, then implement; milestones, labels, commit rules
- `pre-deploy-review` — checklist to run before deploying at the end of a milestone
- `no-real-data` — no real people, events or case content in tests, comments, docs, issues or commits
- `db-migrations` — applying EF Core migrations

## Rules

- No local dotnet: always verify compilation with `docker compose build` after code changes.
- **No real people, events or case content anywhere in the repo** — tests, sample data, code comments,
  SPEC.md, commit messages and GitHub issues. No real names, addresses, companies, incidents, case
  text or attachment file names, even when reproducing a reported bug. Use neutral placeholders
  (`Person A`, `Eksempelsak`, `vedlegg-a.pdf`). The repository is public. See `no-real-data`.
- **Before any ad-hoc fix or change**: update SPEC.md and the GitHub issue first, then implement.
- Make local commits for major changes, referencing the issue. Do not push unless explicitly requested.
- Deploy only after the pre-deployment review findings are fixed or explicitly accepted.

## Project Structure

- `SaksAppWeb/` - Main ASP.NET Core application
- Uses Entity Framework Core with SQLite
- PDF generation via PdfSharpCore
