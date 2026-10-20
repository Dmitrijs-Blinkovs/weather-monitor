# 0012. CI/CD with GitHub Actions, azd and OpenID Connect

- Status: Accepted
- Date: 2026-10-19

## Context

Every change should be built and tested the same way, on a clean machine, before it is merged. Deployments must be
repeatable and must not depend on long-lived Azure credentials stored in GitHub.

## Decision

- **CI** (`.github/workflows/ci.yml`) on every push and on pull requests to `main`, in parallel jobs:
  - .NET: build (warnings as errors), all tests on Microsoft.Testing.Platform with code coverage and TRX reports;
    a coverage summary is written to the run page.
  - Front end: lint, Vitest with coverage, type-check and production build.
  - Browser tests: Playwright against the production build (ADR 0013).
  - Infrastructure: Bicep compile and lint, warnings fail the job.
- **CD** (`.github/workflows/deploy.yml`): `azd provision` + `azd deploy` with OIDC federated credentials
  (`azd pipeline config`), the `production` environment for approvals, and a `/health` smoke test. Manual trigger
  until the first deployment has been made.
- **Dependabot** groups NuGet, npm and GitHub Actions updates weekly; a pull request template carries the definition
  of done.

## Consequences

- A broken build, failing test, inaccessible UI change or invalid template is caught before merge.
- No Azure secret exists in GitHub; access can be revoked by deleting the federated credential.
- Recommended repository settings (not code): protect `main`, require the CI checks, require one review.

## Alternatives considered

- Azure Pipelines: equivalent, but the code already lives on GitHub.
- Publish profiles / service principal secrets: long-lived credentials to rotate and leak.
