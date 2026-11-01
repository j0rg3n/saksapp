---
name: db-migrations
description: Apply Entity Framework Core migrations for SaksApp (SQLite). Use when the schema changes or the database needs updating.
---

# Database migrations

The app uses Entity Framework Core with SQLite. Migrations are applied inside the container:

```bash
dotnet ef database update
```

Schema changes need a SPEC.md update and a GitHub issue first (see `plan-change`). Check migration and
startup safety and backup/restore paths in the `pre-deploy-review` skill before deploying a schema change.
