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

- TODO.md should be lean - reference content in SPEC.md rather than duplicating
- TODO.md should contain ordered groups of tasks
- Tasks within each group can be done in parallel
