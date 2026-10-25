# Weather Monitor - guide for AI agents and contributors

Read this file first. It is the single source of truth for working on this repository, for humans and for any AI
assistant. Tool-specific entry points may link here; assistants that do not discover this file automatically should
be asked to read it. Shared procedures live in `docs/runbooks/` and require no particular AI tool.

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
| [docs/security.md](docs/security.md) | you touch the HTTP pipeline, inputs, outbound calls, identities, secrets or pipelines |
| [docs/performance.md](docs/performance.md) | you change a query, an index, a limit or the chart data - re-run the load test (`tools/load-test`) |
| [docs/runbooks/](docs/runbooks) | you do a routine task - step-by-step procedures for developers and any coding assistant |

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
    Ingestion/                      WeatherIngestionService (fetch all cities in parallel, store attempts), options,
                                    IngestionMetrics (OpenTelemetry meter "WeatherMonitor.Ingestion")
    Retention/                      DataRetentionService (batched purge of old history), options
    FetchLog/                       IFetchLogReader + DTOs (paged log, attempt details with body, cities)
    History/                        WeatherHistoryService (series + per-city summary), IWeatherHistoryReader, rules;
                                    IWeatherReadingsReader (every fetch, paged)
    Paging/                         PageLimits (page 1-1,000, size 1-200), PagedResult<T>
  WeatherMonitor.Infrastructure/  Adapters:
    Persistence/                    WeatherDbContext, configurations, Migrations/, repository, readers,
                                    DatabaseMigrationService (+ read-only grant), DataFreshnessHealthCheck
    OpenWeatherMap/                 OpenWeatherMapClient (typed HttpClient + resilience), options, response DTOs
  WeatherMonitor.Functions/       Isolated worker. FetchWeatherFunction (every minute) and PurgeOldDataFunction (daily)
                                  = thin timer triggers. appsettings.json = cities, retention. host.json = timeout, OTel.
  WeatherMonitor.Web/             MVC API controllers (Controllers/), request models (Models/), Security/ (headers, rate
                                  limiting), Caching/ (output cache, compression), health endpoints, SPA hosting.
  weather-monitor.client/         React 19 + TS + Vite. src/features/{chart,fetch-log}, src/api, src/components, src/hooks,
                                  src/lib, src/test;
                                  e2e/ = Playwright browser tests with a mocked API (e2e/support/mockApi.ts).
                                  vite.config.ts mirrors the server's security headers for `vite preview`.
tests/
  WeatherMonitor.<Project>.Tests/ One test project per production project; Domain tests live in Application.Tests
                                  (xUnit v3, Shouldly, NSubstitute).
tools/load-test/                  Load test: seed a separate LoadTest database with N days, run a second API, measure.
infra/                            Bicep: main.bicep (resource group) + resources.bicep (everything else);
                                  bicepconfig.json = strict linter rules, API versions at most two years old.
azure.yaml                        Azure Developer CLI services: web -> App Service, functions -> Flex Consumption.
.github/                          ci.yml (every push), deploy.yml (after green CI on main, OIDC; credential-free build
                                  job + deploy job; scripts/smoke_test.py checks the new release), dependabot.yml, PR template.
                                  Actions pinned to commit SHAs.
```

## Architecture in one screen

- **Dependency rule:** Domain <- Application <- Infrastructure <- hosts (Functions, Web). Inner layers never reference
  outer ones. Application defines interfaces (ports); Infrastructure implements them; hosts only compose
  (`AddWeatherIngestion`, `AddWeatherHistory`, `AddDataRetention`, `AddPersistence`, `AddPersistenceHealthChecks`,
  `AddOpenWeatherMap`).
- **Write path:** timer -> `WeatherIngestionService` -> `IWeatherProvider` (one call per city, in parallel) ->
  `FetchAttempt.Succeeded/Failed` -> `IFetchAttemptRepository.AddRangeAsync` (one transaction for the run).
- **Read path:** controller -> `IFetchLogReader` / `IWeatherHistoryService` / `IWeatherReadingsReader` -> EF Core
  projections (no tracking; lists never load the payload, only `GET /api/logs/{id}` returns one attempt's body).
- **Expected failures are values, not exceptions:** the provider returns `WeatherFetchResult.Failure(...)`; only bugs throw.
- **Domain and data model describe the same data:** EF configurations use the domain's constants, and
  `DataModelContractTests` fails when a text column's length and its domain rule drift apart (or a new text column has
  no decided length). Metrics count *stored* attempts, so a failing database also triggers the data alert.
- **Business rules live in the model:** invariants in Domain (`FetchAttempt`, `CurrentWeather` - plausibility, country,
  status codes), use-case rules in Application (`WeatherHistoryRules`, `PageLimits`). Adapters
  (`CurrentWeather.TryCreate`) and request models (`[Range(1, PageLimits.MaxPage)]`) reuse them, never copy them.
  `DependencyRuleTests` and `ArchitectureTests` fail if a layer reaches outwards.
- **Time is UTC everywhere** (`DateTime` with `Kind=Utc`, `...Utc` suffix); an EF converter restores the kind on read
  and rejects `Kind=Unspecified`, also in query parameters.
- **Observability:** OpenTelemetry in both hosts (exported only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set),
  ingestion metrics, `/health/live` (process) vs. `/health` (database + a successful fetch within 10 minutes for every city fetched in the last day).
- **Security:** every input is capped, every response carries strict security headers, `/api/*` is rate-limited and
  cached, provider responses are size-capped and validated, and each Azure app has its own least-privilege identity
  ([docs/security.md](docs/security.md)).

## Commands

Run from the repository root unless stated otherwise.

```bash
dotnet build WeatherMonitor.slnx                                  # warnings are errors
dotnet test --solution WeatherMonitor.slnx                        # all .NET tests (Microsoft.Testing.Platform syntax)
dotnet test --project tests/WeatherMonitor.Web.Tests              # one test project
cd src/weather-monitor.client && npm ci && npm run lint && npm test && npm run build
cd src/weather-monitor.client && npm audit --omit=dev --audit-level=high         # as in CI
cd src/weather-monitor.client && npm run test:e2e:install && npm run test:e2e   # Playwright (builds + previews itself)
dotnet test --solution WeatherMonitor.slnx --coverage --coverage-output-format cobertura --report-trx  # as in CI
az bicep build --file infra/main.bicep --stdout > /dev/null                      # validate the Azure templates
dotnet run --project src/WeatherMonitor.Functions                 # needs Azurite + API key (see getting-started)
dotnet run --project src/WeatherMonitor.Web --launch-profile https # https://localhost:7180, forwards to Vite :5173
dotnet ef migrations add <Name> --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure --output-dir Persistence/Migrations
```

Local endpoints: Web `https://localhost:7180` / `http://localhost:5180` (API reference `/scalar`), Vite `http://127.0.0.1:5173`, Functions host
`http://localhost:7071`, Azurite `10000-10002`, database `(localdb)\MSSQLLocalDB` / `WeatherMonitor`.
Health: `http://localhost:5180/health/live` (process) and `/health` (`Degraded` until the first successful fetch).
Trigger a timer immediately: `POST http://localhost:7071/admin/functions/FetchWeather` (or `PurgeOldData`) with body
`{"input":""}`.

## Conventions

**C#**
- File-scoped namespaces, primary constructors, `sealed` by default, `internal` for implementations (tests use
  `InternalsVisibleTo`). Private fields `_camelCase`; `const` and `static readonly` fields PascalCase.
  `.editorconfig` is enforced in the build, naming rules included (`dotnet_diagnostic.IDE1006.severity`).
- Logging through `[LoggerMessage]` source-generated methods, never string interpolation in log calls.
- Options classes bind a named section; those with rules validate with DataAnnotations/`IValidatableObject` and
  `ValidateOnStart()`.
- New package: add `<PackageVersion>` to `Directory.Packages.props`, then a version-less `<PackageReference>`.
  High/critical NuGet advisories (also transitive) fail the build; pin a fixed transitive version there if needed.
- Never put secrets in files under source control. The OpenWeatherMap key lives in user secrets locally.
  `<UserSecretsId>` in the Functions `.csproj` is only the name of that local folder, not a secret. The Web project
  has no secrets (Windows authentication to LocalDB), so it has no `UserSecretsId`.

**Tests**
- Name tests as sentences (`A_failed_city_is_logged_as_a_failure_while_the_others_are_still_stored`).
- Pass `TestContext.Current.CancellationToken` to anything that takes a token (analyzer xUnit1051 is an error).
- Persistence tests use in-memory SQLite with the real model (`SqliteTestDatabase`), API tests use
  `WeatherApiFactory` (real pipeline, substituted services). No test needs SQL Server, network or Azure.

**Front end**
- Feature folders (`src/features/<feature>`), shared code in `src/lib`, `src/components`, `src/hooks`, `src/api`.
- Components get the API from `WeatherApiContext` (tests inject `fakeApi()`); server state via TanStack Query.
- Colours are CSS tokens in `src/index.css`; series colours are `--series-1..8` in fixed order, assigned per city by
  `colorForCity` (never by position). Text never uses a series colour.
- Tests sit next to the code (`*.test.ts(x)`), Vitest + Testing Library; `src/test` has fixtures and `renderWithProviders`.

## Definition of done

1. `dotnet build WeatherMonitor.slnx` - 0 warnings, 0 errors.
2. `dotnet test --solution WeatherMonitor.slnx` and `npm audit --omit=dev --audit-level=high && npm run lint && npm test
   && npm run test:e2e && npm run build` in the client - green (CI runs these, with coverage and a Release build, plus the Bicep
   build and a migrations up/down job on SQL Server).
3. New behaviour has tests at the lowest layer that can prove it; bugs get a test that fails without the fix.
4. A data model change has a migration; an API change is reflected in `src/api/types.ts` and the README API table.
5. A changed design decision has a new ADR in `docs/adr/`, linked from the record it changes; a changed security control is reflected in
   `docs/security.md`.
6. User-visible change: run the app (`docs/runbooks/run-locally.md`) and look at it.

## Known pitfalls

- Everything in the repository is English: code, comments, docs, UI text, and date/number formatting (`UI_LOCALE`
  in `src/lib/format.ts`). Never format with the browser's default locale.
- `.gitignore` (VS template) ignores folders named `log`/`logs` - that is why the UI feature folder is `fetch-log`.
- A deep clone path on Windows breaks the Functions build (`MSB3030 ... WorkerExtensions ... not found`, "Filename too
  long"): the worker extension build nests deep paths. Clone to a short path such as `C:\src\weather-monitor`.
- Vite (`npm run dev`, `vite preview`), the SPA proxy and Playwright use `127.0.0.1`, not `localhost`, on purpose:
  Node binds `localhost` to IPv6 `::1` only, and where IPv6 loopback is blocked (VPN or security software)
  connections to `::1` hang - the SPA proxy then logs `Unable to connect to the SPA development server` and
  Playwright times out waiting for its web server. Keep `server.host`/`preview.host` and `SpaProxyServerUrl` on IPv4.
- An API contract change must be mirrored in `src/api/types.ts` and in `e2e/support/mockApi.ts`.
- `q=Rome` resolves to Rome, Georgia, USA. Every city in `Weather:Cities` needs its `CountryCode`.
- OpenWeatherMap refreshes an observation about every 10 minutes; fetching every minute stores duplicates on purpose.
  The history reader collapses them with `DISTINCT` for the chart; the readings table shows them on purpose (proof that
  each fetch arrived) - do not "fix" the duplicates in the writer.
- The API key travels in the query string: keep `RemoveAllLoggers()` on the client and `Redact(...)` on messages.
- `[Produces("application/json")]` on controllers turns RFC 7807 errors into `application/json` - do not add it.
- Unknown `/api/*` routes must stay 404 (`app.MapMethods("/api/{**path}", [GET, HEAD], ...)` before `MapFallbackToFile`, so a
  wrong verb on a known route still gets 405).
- `DateTime` with `Kind=Unspecified` is rejected by the domain and by `UtcDateTimeConverter` (writes and query
  parameters) on purpose: read as local time it would shift by the machine's offset. SQLite cannot order `DateTimeOffset`,
  which is one reason the model uses UTC `DateTime`.
- xUnit v3 runs on Microsoft.Testing.Platform: use `dotnet test --solution/--project`, not the old VSTest switches.
- In development the UI is served by Vite on 5173; deep links typed directly on 5180 return 404 (only `/` redirects).
- Core Tools warns against `func start` for isolated projects; use `dotnet run` (works from any folder).
- A brand-new OpenWeatherMap key returns 401 for up to ~2 hours.
- Output caching: two identical API requests within the cache window return the same body. Web tests that change the
  substituted data between requests must vary the query (or the test fails for a "wrong" reason).
- Fetch-log and readings paging is a deferred join (`FetchLogReader`, `WeatherReadingsReader`): OFFSET over index
  keys, then the page's rows. Keep the subquery inside the lambda, or Skip/Take become SQL constants (one plan per page). Check a query change on SQL
  Server, not only SQLite: the tests prove results, not plans.
- Output-cache locking (request coalescing) is off on purpose: with it, a request that waits for an identical one
  inherits that request's cancellation and returns 500 when the browser aborts the first (React StrictMode always does).
- The CSP is defined twice: `Web/Security/SecurityHeaders.cs` (production; the development-only `/scalar` page has its
  own, `ApiReferenceContentSecurityPolicy`) and `vite.config.ts` (`vite preview` for
  Playwright). Change both; inline `<script>`/`style=""` attributes violate it (React `style` props are fine).
- `PageLimits.MaxPage` (Application) and `MAX_PAGE` (client `components/Pagination.tsx`) must stay equal.
- `/health` is rate-limited and depends on data freshness; probes that only need "process alive" use `/health/live`.
- Two managed identities in Azure: the web app can only read the database (`db_datareader`, granted by the function
  after migrating). Anything the web app must write needs a deliberate change (ADR 0015).
- OpenTelemetry in the Functions worker is wired up only with an Application Insights connection string: with
  `UseFunctionsWorkerDefaults()` the host stops forwarding worker logs, so locally the console would go silent.
- `workflow_run` branch filters match only the branch name; the deploy workflow also checks that the CI run was a
  push to this repository. Keep that check.

## Where to start for common tasks

| Task | Start here | Runbook |
|---|---|---|
| Run / demo / screenshot the app | `docs/getting-started.md` | `run-locally` |
| Check a change before committing | Definition of done above | `verify-changes` |
| Add or change a city | `src/WeatherMonitor.Functions/appsettings.json` | `manage-cities` |
| Change stored data | `src/WeatherMonitor.Domain`, `Infrastructure/Persistence/Configurations` | `add-ef-migration` |
| New endpoint or field in the UI | `Web/Controllers`, `Application/<feature>`, `client/src/api` | `add-api-endpoint` |
| Fetches failing / no data | Fetch log page, `FetchAttempts` table | `troubleshoot-ingestion` |
| Deploy to Azure / change infrastructure | `docs/deployment.md`, `infra/`, `.github/workflows/deploy.yml` | `deploy-to-azure` |
| Security review / new input or header | `docs/security.md`, `Web/Security`, `OpenWeatherMapClient` | `verify-changes` |

## Contributor safeguards

- Never read, print or ask for the OpenWeatherMap API key. Check presence only using the filtered command in
  `docs/runbooks/run-locally.md`; never expose the output of an unfiltered secrets listing.
- Do not create or change Azure resources, commit or push unless the user explicitly asks for it.
- Keep repository code, comments, documentation and UI text in English.
- `.claude/` and `CLAUDE.md` are optional Claude integration files, not application dependencies.
  Shared project knowledge belongs in this file and `docs/`, not only in a tool-specific directory.
