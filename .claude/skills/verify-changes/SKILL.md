---
name: verify-changes
description: Run the full Weather Monitor quality gate exactly as CI does (.NET build with warnings as errors and tests with coverage, front-end lint, unit tests, type-check and production build, Playwright browser tests, Bicep compile) and interpret failures. Use before saying a change is done, before a commit or pull request, and after resolving merge conflicts.
---

# Verify a change

Run every step from the repository root and report the actual numbers. A step that was not run is reported as not run.
CI (`.github/workflows/ci.yml`) runs the same steps on every push.

## 1. Stop running hosts

Running Functions/Web hosts lock their DLLs and make the build fail with MSB3026/MSB3027. Stop them first.

## 2. .NET

```bash
dotnet build WeatherMonitor.slnx --configuration Release
dotnet test --solution WeatherMonitor.slnx --configuration Release --no-build --coverage --coverage-output-format cobertura --report-trx
```

- Expect `0 Warning(s)` and `0 Error(s)`: `TreatWarningsAsErrors` is on, so analyzer and style findings fail the build.
  Fix the code; do not suppress warnings or edit `Directory.Build.props` to get green.
- `xUnit1051` -> pass `TestContext.Current.CancellationToken`.
- `NU190x` (vulnerable package) -> pin a fixed version in `Directory.Packages.props` ("Transitive security pins").
- "Testing with VSTest target is no longer supported" -> use `--solution`/`--project` (Microsoft.Testing.Platform).
- `The_migrations_are_in_sync_with_the_model` failing -> add a migration (`add-ef-migration` skill).
- `MSB3030 ... WorkerExtensions ... not found` on a fresh clone -> the path is too long; clone to a short path.

## 3. Front end

```bash
cd src/weather-monitor.client
npm ci                     # only when package-lock.json changed or node_modules is missing
npm run lint
npm run test:coverage      # Vitest unit/component tests
npm run build              # tsc -b (app, tests, e2e) + Vite production build
npm run test:e2e:install   # once per machine (Chromium)
npm run test:e2e           # Playwright against the production build with a mocked API
```

Expect no lint output, all tests passing, no "chunks are larger than 500 kB" warning (pages are lazy-loaded). Playwright
failures leave a report: `npx playwright show-report`. An axe violation is a real accessibility bug - fix the UI.

## 4. Infrastructure (when `infra/` changed)

```bash
az bicep build --file infra/main.bicep --stdout > /dev/null
```

Must compile without warnings. This only compiles locally; it never touches a subscription.

## 5. Behaviour

- Each new behaviour has a test at the lowest layer that proves it; a bug fix has a test that fails without the fix.
- Model change -> a migration exists. API change -> `src/api/types.ts`, `e2e/support/mockApi.ts` and the README API
  table are updated.
- User-visible change -> run the app (`run-locally` skill) and look at it; tests alone do not prove UI work.

## 6. Report

State: build result, .NET test totals, front-end and Playwright totals, lint/build/Bicep result, what was checked in the
running app, and anything not verified.
