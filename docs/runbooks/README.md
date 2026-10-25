# Maintenance runbooks

These procedures are shared project documentation for developers and coding assistants.
No AI product is required. Run shell commands from the repository root unless a step says otherwise.

| Task | Procedure |
|---|---|
| Start and verify all local services | [Run locally](run-locally.md) |
| Build and check changes | [Verify changes](verify-changes.md) |
| Add or change a read API and its UI | [Add an API endpoint](add-api-endpoint.md) |
| Change the database schema | [Add an EF migration](add-ef-migration.md) |
| Add, remove or rename monitored cities | [Manage cities](manage-cities.md) |
| Diagnose failed or missing weather fetches | [Troubleshoot ingestion](troubleshoot-ingestion.md) |
| Provision or deploy to Azure | [Deploy to Azure](deploy-to-azure.md) |

Start with [getting started](../getting-started.md) on a new machine and
[the review guide](../review-guide.md) when evaluating the assignment.
Architecture and the reasons for design choices live in [architecture](../architecture.md)
and [ADRs](../adr/README.md). Contributor rules live in [AGENTS.md](../../AGENTS.md).

The root `CLAUDE.md`, `.claude/skills/` wrappers and `.claude/launch.json` are optional
development-tool integration. The wrappers link here rather than maintaining a second copy
of these procedures. They are not needed by the application, tests or CI.
