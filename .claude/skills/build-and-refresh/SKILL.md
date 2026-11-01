---
name: build-and-refresh
description: Build SaksApp in Docker to verify it compiles, run the tests, and refresh the running container. Use after any code change, since dotnet is not installed on the host.
---

# Build, test and refresh

There is no local dotnet. Everything goes through Docker.

1. **Verify compilation** (required after any code change):
   ```bash
   docker compose build
   ```
2. **Run tests**:
   ```bash
   docker compose --profile test run --rm test
   ```
   If new `.cs` files were added to `SaksAppWeb.Tests`, rebuild the test image first with
   `docker compose --profile test build test --no-cache`, or the new files are not picked up.
3. **Refresh the running container** once the build succeeds:
   ```bash
   docker compose up -d --force-recreate
   ```

Report build or test failures with the actual output. Do not claim success without having run the build.

Tests and sample data must never contain real people, events or case content. See the `no-real-data` skill.
