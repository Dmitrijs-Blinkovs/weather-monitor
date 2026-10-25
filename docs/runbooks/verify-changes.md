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
- Expect one skipped test: the `Category=SqlServer` tests need `WEATHERMONITOR_TEST_SQLSERVER` (section 4 runs them).
- `xUnit1051` -> pass `TestContext.Current.CancellationToken`.
- `NU1903`/`NU1904` errors (high/critical vulnerable package, also transitive) -> update it or pin a fixed version in
  `Directory.Packages.props` ("Transitive security pins"). `NU1901`/`NU1902` stay warnings.
- "Testing with VSTest target is no longer supported" -> use `--solution`/`--project` (Microsoft.Testing.Platform).
- `The_migrations_are_in_sync_with_the_model` failing -> add a migration (`add-ef-migration` runbook).
- `MSB3030 ... WorkerExtensions ... not found` on a fresh clone -> the path is too long; clone to a short path.

## 3. Front end

```bash
cd src/weather-monitor.client
npm ci                     # only when package-lock.json changed or node_modules is missing
npm audit --omit=dev --audit-level=high   # production dependencies, as in CI
npm run lint
npm run test:coverage      # Vitest unit/component tests
npm run build              # tsc -b (app, tests, e2e) + Vite production build
npm run test:e2e:install   # once per machine (Chromium)
npm run test:e2e           # Playwright against the production build with a mocked API
```

Expect no lint output, all tests passing, no "chunks are larger than 500 kB" warning (pages are lazy-loaded). Playwright
failures leave a report: `npx playwright show-report`. An axe violation is a real accessibility bug - fix the UI. A CSP
violation means new inline script/style or a new origin: fix the code, or change the policy in both
`Web/Security/SecurityHeaders.cs` and `vite.config.ts` and record why in `docs/security.md`.

## 4. Deployment checks and infrastructure

Run the smoke-check regression tests (no network or Azure):

```bash
python -B -m unittest discover -s .github/scripts -p 'test_*.py' -v
```

When `infra/` changed, also compile Bicep:

```bash
az bicep build --file infra/main.bicep --stdout > /dev/null
```

Must compile without warnings. This only compiles locally; it never touches a subscription.

Migrations: CI also applies all migrations to SQL Server 2022, rolls back to `0` and applies them again. Locally the same
check needs a throwaway database on LocalDB (never the `WeatherMonitor` database with the user's data):

```bash
dotnet ef database update --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure --connection "Server=(localdb)\MSSQLLocalDB;Database=WeatherMonitor_Verify;Trusted_Connection=True;TrustServerCertificate=True"
```

Then `database update 0` and again the latest, and drop `WeatherMonitor_Verify` afterwards.

The same CI job runs the opt-in SQL Server tests (two hosts creating a missing database at once). Locally, on LocalDB
(each test creates and drops its own `WeatherMonitorTest_<guid>` database):

```bash
WEATHERMONITOR_TEST_SQLSERVER='Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True' dotnet test --project tests/WeatherMonitor.Infrastructure.Tests --filter-trait "Category=SqlServer"
```

## 5. Behaviour

- Each new behaviour has a test at the lowest layer that proves it; a bug fix has a test that fails without the fix.
- Model change -> a migration exists. API change -> `src/api/types.ts`, `e2e/support/mockApi.ts` and the README API
  table are updated.
- Security-relevant change (new input, header, outbound call, identity, secret, workflow) -> `docs/security.md` is
  still true.
- User-visible change -> run the app (`run-locally` runbook) and look at it; tests alone do not prove UI work.

## 6. Report

State: build result, .NET test totals, front-end and Playwright totals, lint/build/Bicep result, what was checked in the
running app, and anything not verified.
