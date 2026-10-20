# Weather Monitor - guide for AI agents and contributors

Read this file first. It is the single source of truth for working on this repository, for humans and for any AI
assistant (Claude Code reads it through `CLAUDE.md`; Codex, Cursor, Copilot and others read `AGENTS.md` directly).

## What this is

An Azure Function fetches the current weather for London (GB), Rome (IT) and Riga (LV) from OpenWeatherMap **every
minute**, stores the full JSON payload plus a log of every attempt (success or failure) in SQL Server via EF Core, and an
ASP.NET Core MVC API with a React + TypeScript UI shows the fetch log and a temperature chart (all cities on one chart,
min/max band, country, temperature, last update time, date and time filtering).

The original assignment and how each requirement is met: [README.md](README.md). Deeper material:

| Document | Read it when |
|---|---|
| [docs/architecture.md](docs/architecture.md) | you change behaviour: components, data model, API contracts, front end, configuration |
| [docs/adr/](docs/adr/README.md) | you are about to change a design decision - read why it was made first |
| [docs/getting-started.md](docs/getting-started.md) | you set the environment up from scratch (with or without an AI assistant) |
| [docs/debugging-in-visual-studio.md](docs/debugging-in-visual-studio.md) | you run and debug manually in Visual Studio 2026 |
| [docs/deployment.md](docs/deployment.md) | you deploy to Azure or touch `infra/`, `azure.yaml` or the workflows |
| [.claude/skills/](.claude/skills) | you do a routine task - each skill is a step-by-step runbook any agent can follow |

## Repository map

```
WeatherMonitor.slnx               Solution (new XML format). WeatherMonitor.slnLaunch = VS "Web + Functions" profile.
Directory.Build.props             net10.0, nullable, warnings as errors, analyzers - applies to every project.
Directory.Packages.props          Central package versions (+ transitive security pins). Versions live ONLY here.
global.json                       SDK 10.0.1xx+, and the Microsoft.Testing.Platform test runner (needed by xUnit v3).
src/
  WeatherMonitor.Domain/          FetchAttempt (aggregate root) owns WeatherSnapshot; CurrentWeather value object.
  WeatherMonitor.Application/     Use cases + ports:
    Abstractions/                   IWeatherProvider, IFetchAttemptRepository, WeatherFetchResult
    Ingestion/                      WeatherIngestionService (fetch all cities in parallel, store attempts), options
    FetchLog/                       IFetchLogReader + DTOs (paged log, cities)
    History/                        WeatherHistoryService (series + per-city summary), IWeatherHistoryReader, rules
  WeatherMonitor.Infrastructure/  Adapters:
    Persistence/                    WeatherDbContext, configurations, Migrations/, repository, readers
    OpenWeatherMap/                 OpenWeatherMapClient (typed HttpClient + resilience), options, response DTOs
  WeatherMonitor.Functions/       Isolated worker. FetchWeatherFunction = thin timer trigger. appsettings.json = cities.
  WeatherMonitor.Web/             MVC API controllers (Controllers/), request models (Models/), SPA hosting.
  weather-monitor.client/         React 19 + TS + Vite. src/features/{chart,fetch-log}, src/api, src/lib, src/test;
                                  e2e/ = Playwright browser tests with a mocked API (e2e/support/mockApi.ts).
tests/
  WeatherMonitor.<Project>.Tests/ One test project per production project (xUnit v3, Shouldly, NSubstitute).
infra/                            Bicep: main.bicep (resource group) + resources.bicep (everything else).
azure.yaml                        Azure Developer CLI services: web -> App Service, functions -> Flex Consumption.
.github/                          ci.yml (every push), deploy.yml (manual, OIDC), dependabot.yml, PR template.
```

## Architecture in one screen

- **Dependency rule:** Domain <- Application <- Infrastructure <- hosts (Functions, Web). Inner layers never reference
  outer ones. Application defines interfaces (ports); Infrastructure implements them; hosts only compose
  (`AddWeatherIngestion`, `AddWeatherHistory`, `AddPersistence`, `AddOpenWeatherMap`).
- **Write path:** timer -> `WeatherIngestionService` -> `IWeatherProvider` (one call per city, in parallel) ->
  `FetchAttempt.Succeeded/Failed` -> `IFetchAttemptRepository.AddRangeAsync` (one transaction for the run).
- **Read path:** controller -> `IFetchLogReader` / `IWeatherHistoryService` -> EF Core projections (no tracking, no payload).
- **Expected failures are values, not exceptions:** the provider returns `WeatherFetchResult.Failure(...)`; only bugs throw.
- **Time is UTC everywhere** (`DateTime` with `Kind=Utc`, `...Utc` suffix); an EF converter restores the kind on read.

## Commands

Run from the repository root unless stated otherwise.

```bash
dotnet build WeatherMonitor.slnx                                  # warnings are errors
dotnet test --solution WeatherMonitor.slnx                        # all .NET tests (Microsoft.Testing.Platform syntax)
dotnet test --project tests/WeatherMonitor.Web.Tests              # one test project
cd src/weather-monitor.client && npm ci && npm run lint && npm test && npm run build
cd src/weather-monitor.client && npm run test:e2e:install && npm run test:e2e   # Playwright (builds + previews itself)
dotnet test --solution WeatherMonitor.slnx --coverage --coverage-output-format cobertura --report-trx  # as in CI
az bicep build --file infra/main.bicep --stdout > /dev/null                      # validate the Azure templates
dotnet run --project src/WeatherMonitor.Functions                 # needs Azurite + API key (see getting-started)
dotnet run --project src/WeatherMonitor.Web --launch-profile https # https://localhost:7180, forwards to Vite :5173
dotnet ef migrations add <Name> --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure --output-dir Persistence/Migrations
```

Local endpoints: Web `https://localhost:7180` / `http://localhost:5180`, Vite `http://localhost:5173`, Functions host
`http://localhost:7071`, Azurite `10000-10002`, database `(localdb)\MSSQLLocalDB` / `WeatherMonitor`.
Trigger the timer immediately: `POST http://localhost:7071/admin/functions/FetchWeather` with body `{"input":""}`.

## Conventions

**C#**
- File-scoped namespaces, primary constructors, `sealed` by default, `internal` for implementations (tests use
  `InternalsVisibleTo`). Private fields `_camelCase`. `.editorconfig` is enforced in the build.
- Logging through `[LoggerMessage]` source-generated methods, never string interpolation in log calls.
- Options classes bind a named section, validate with DataAnnotations/`IValidatableObject` and `ValidateOnStart()`.
- New package: add `<PackageVersion>` to `Directory.Packages.props`, then a version-less `<PackageReference>`.
- Never put secrets in files under source control. The OpenWeatherMap key lives in user secrets locally.

**Tests**
- Name tests as sentences (`A_failed_city_is_logged_as_a_failure_while_the_others_are_still_stored`).
- Pass `TestContext.Current.CancellationToken` to anything that takes a token (analyzer xUnit1051 is an error).
- Persistence tests use in-memory SQLite with the real model (`SqliteTestDatabase`), API tests use
  `WeatherApiFactory` (real pipeline, substituted services). No test needs SQL Server, network or Azure.

**Front end**
- Feature folders (`src/features/<feature>`), shared code in `src/lib`, `src/components`, `src/api`.
- Components get the API from `WeatherApiContext` (tests inject `fakeApi()`); server state via TanStack Query.
- Colours are CSS tokens in `src/index.css`; series colours are `--series-1..8` in fixed order, assigned per city by
  `colorForCity` (never by position). Text never uses a series colour.
- Tests sit next to the code (`*.test.ts(x)`), Vitest + Testing Library; `src/test` has fixtures and `renderWithProviders`.

## Definition of done

1. `dotnet build WeatherMonitor.slnx` - 0 warnings, 0 errors.
2. `dotnet test --solution WeatherMonitor.slnx` and `npm run lint && npm test && npm run test:e2e && npm run build` in the
   client - green (CI runs exactly these, plus the Bicep build).
3. New behaviour has tests at the lowest layer that can prove it; bugs get a test that fails without the fix.
4. A data model change has a migration; an API change is reflected in `src/api/types.ts` and the README API table.
5. A changed design decision has a new or superseding ADR in `docs/adr/`.
6. User-visible change: run the app (`.claude/skills/run-locally`) and look at it.

## Known pitfalls

- Everything in the repository is English: code, comments, docs, UI text, and date/number formatting (`UI_LOCALE`
  in `src/lib/format.ts`). Never format with the browser's default locale.
- `.gitignore` (VS template) ignores folders named `log`/`logs` - that is why the UI feature folder is `fetch-log`.
- A deep clone path on Windows breaks the Functions build (`MSB3030 ... WorkerExtensions ... not found`, "Filename too
  long"): the worker extension build nests deep paths. Clone to a short path such as `C:\src\weather-monitor`.
- A Web host started in the background can leave the SPA-proxy-launched Vite hanging (`Unable to connect to the SPA
  development server`); start Vite yourself first (`.claude/skills/run-locally`).
- An API contract change must be mirrored in `src/api/types.ts` and in `e2e/support/mockApi.ts`.
- `q=Rome` resolves to Rome, Georgia, USA. Every city in `Weather:Cities` needs its `CountryCode`.
- OpenWeatherMap refreshes an observation about every 10 minutes; fetching every minute stores duplicates on purpose.
  The history reader collapses them with `DISTINCT` - do not "fix" the duplicates in the writer.
- The API key travels in the query string: keep `RemoveAllLoggers()` on the client and `Redact(...)` on messages.
- `[Produces("application/json")]` on controllers turns RFC 7807 errors into `application/json` - do not add it.
- Unknown `/api/*` routes must stay 404 (`app.Map("/api/{**path}", ...)` before `MapFallbackToFile`).
- `DateTime` with `Kind=Unspecified` is rejected by the domain on purpose. SQLite cannot order `DateTimeOffset`,
  which is one reason the model uses UTC `DateTime`.
- xUnit v3 runs on Microsoft.Testing.Platform: use `dotnet test --solution/--project`, not the old VSTest switches.
- In development the UI is served by Vite on 5173; deep links typed directly on 5180 return 404 (only `/` redirects).
- Core Tools warns against `func start` for isolated projects; use `dotnet run` (works from any folder).
- A brand-new OpenWeatherMap key returns 401 for up to ~2 hours.

## Where to start for common tasks

| Task | Start here | Skill |
|---|---|---|
| Run / demo / screenshot the app | `docs/getting-started.md` | `run-locally` |
| Check a change before committing | Definition of done above | `verify-changes` |
| Add or change a city | `src/WeatherMonitor.Functions/appsettings.json` | `manage-cities` |
| Change stored data | `src/WeatherMonitor.Domain`, `Infrastructure/Persistence/Configurations` | `add-ef-migration` |
| New endpoint or field in the UI | `Web/Controllers`, `Application/<feature>`, `client/src/api` | `add-api-endpoint` |
| Fetches failing / no data | Fetch log page, `FetchAttempts` table | `troubleshoot-ingestion` |
| Deploy to Azure / change infrastructure | `docs/deployment.md`, `infra/`, `.github/workflows/deploy.yml` | `deploy-to-azure` |
