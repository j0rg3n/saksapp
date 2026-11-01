---
name: pre-deploy-review
description: Pre-deployment review checklist for SaksApp, to run at the end of a milestone before deploying. Covers security, reliability, stability, responsiveness, performance and code smells.
---

# Pre-deployment review

Run before deploying at the end of a milestone. Review the changes going out (diff since the last
deployed milestone) across **all** of:

- **Security** — authn/authz gaps, injection, secrets or tokens at rest, CSRF, IDOR, data exposure
- **Reliability** — error handling, retries, partial failure, migration and startup safety, backup/restore paths
- **Stability** — regressions, breaking schema or API changes, resource exhaustion (disk, memory, handles)
- **Responsiveness** — UI latency, blocking calls on request paths, phone and desktop layouts
- **Performance** — N+1 queries, unbounded result sets, work done in memory that belongs in the query
- **Code smells** — duplication, drift between related call sites, dead code, tests that pass for the wrong reason
- **Real data leakage** — real people, events or case content in tests, comments, SPEC.md, commits or issues (see `no-real-data`)

## Recording findings

Record anything found in SPEC.md and as GitHub issues (in the milestone being deployed) **before**
fixing it, following the `plan-change` skill. Issues are public: no real case content.

## Gate

Deploy only once every finding is either fixed or explicitly accepted by the user.

`/security-review` and `/code-review` cover parts of this; neither is a substitute for the whole list.
