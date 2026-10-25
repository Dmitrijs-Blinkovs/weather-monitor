# 0012. CI/CD with GitHub Actions, azd and OpenID Connect

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
  (`azd pipeline config`), the `production` environment for approvals, and a smoke test (`Healthy`, plus a new successful fetch of every city
  after both deployments completed, ADR 0017). Manual trigger
  until the first deployment has been made.
- **Dependabot** updates NuGet, npm (both grouped) and GitHub Actions weekly (version updates paused,
  [0019](0019-dependabot-version-updates-paused.md)); a pull request template carries the definition of done.

## Consequences

- A broken build, failing test, inaccessible UI change or invalid template is caught before merge.
- No Azure secret exists in GitHub; access can be revoked by deleting the federated credential.
- Recommended repository settings (not code): protect `main`, require the CI checks, require one review.

## Build and deploy jobs

- The deployment is split into a **build job without any credential** (restore, `npm ci`, `dotnet publish`, upload) and a
  **deploy job with the OIDC token** that runs no build tooling (`azd provision`, `azd deploy --from-package`). A
  malicious npm lifecycle script or MSBuild target therefore never sees an Azure token or the API key.
- Deployments run after a green CI run on `main` (`workflow_run`) or manually from `main`, and are skipped until the
  Azure variables exist. The API key is an environment secret of `production`, visible to the provision step only.
- All actions are pinned to commit SHAs (Dependabot updates them while its version updates run), checkouts do not
  persist credentials, high and critical NuGet advisories fail the build, `npm audit` gates production dependencies,
  Dependabot waits 3 days before adopting a release, and a CI job applies and rolls back all migrations on a real SQL
  Server.

## Alternatives considered

- Azure Pipelines: equivalent, but the code already lives on GitHub.
- Publish profiles / service principal secrets: long-lived credentials to rotate and leak.
