---
name: verify-changes
description: Run the full Weather Monitor quality gate (.NET build with warnings as errors, all .NET tests, front-end lint, tests, type-check and production build) and interpret failures. Use before saying a change is done, before a commit or pull request, and after resolving merge conflicts.
---

# Verify a change

Run every step from the repository root and report the actual numbers. A step that was not run is reported as not run.

## 1. Stop running hosts

Running Functions/Web hosts lock their DLLs and make the build fail with MSB3026/MSB3027. Stop them first.

## 2. .NET

```bash
dotnet build WeatherMonitor.slnx --configuration Release
dotnet test --solution WeatherMonitor.slnx --configuration Release --no-build
```

- Expect `0 Warning(s)` and `0 Error(s)`: `TreatWarningsAsErrors` is on, so analyzer and style findings fail the build.
  Fix the code; do not suppress warnings or edit `Directory.Build.props` to get green.
- `xUnit1051` -> pass `TestContext.Current.CancellationToken`.
- `NU190x` (vulnerable package) -> pin a fixed version in `Directory.Packages.props` ("Transitive security pins").
- "Testing with VSTest target is no longer supported" -> you used old `dotnet test` switches; use `--solution`/`--project`.

## 3. Front end

```bash
cd src/weather-monitor.client
npm ci            # only when package-lock.json changed or node_modules is missing
npm run lint
npm test
npm run build     # runs tsc -b (type-check incl. tests) and the Vite production build
```

Expect no lint output, all tests passing, and no "chunks are larger than 500 kB" warning (pages are lazy-loaded; keep it
that way).

## 4. Behaviour

- Each new behaviour has a test at the lowest layer that proves it; a bug fix has a test that fails without the fix.
- Model change -> a migration exists (`add-ef-migration` skill). API change -> `src/api/types.ts` and README API table updated.
- User-visible change -> run the app (`run-locally` skill) and look at it; tests alone do not prove UI work.

## 5. Report

State: build result, .NET test totals, front-end test totals, lint/build result, what was checked in the running app,
and anything not verified.
