---
name: verify-changes
description: Run the full Weather Monitor quality gate exactly as CI does (.NET build with warnings as errors and tests with coverage, front-end lint, unit tests, type-check and production build, Playwright browser tests, Bicep compile) and interpret failures. Use before saying a change is done, before a commit or pull request, and after resolving merge conflicts.
---

Follow the shared project runbook at
[docs/runbooks/verify-changes.md](../../../docs/runbooks/verify-changes.md).

Run commands from the repository root. The runbook is the source of truth;
do not duplicate its instructions here.
