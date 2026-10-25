# Assignment review guide

## Start here

1. Follow [getting started](getting-started.md) to install prerequisites and set your own
   OpenWeatherMap key using .NET user secrets. Never put a key in the repository.
2. Run Azurite, the Functions host and the Web host as described in
   [run locally](runbooks/run-locally.md). The Web host starts the React development server.
3. Open `http://localhost:5180/`, which forwards to `http://127.0.0.1:5173`.
4. Check **Fetch log** for successful attempts for London, Rome and Riga; open a row's
   details to inspect the full payload. A provider failure is recorded as a failed attempt.
5. Check **Temperature** for all three cities, their country, temperatures and last update
   times. Try the presets and a custom date/time range. A new database starts empty;
   allow several provider updates to accumulate for a meaningful line chart.

`/health/live` checks the Web process. `/health` also checks database access and recent
successful ingestion. Automated browser tests use a mocked API: they do not prove that
the live provider key works. Complete the live checks above separately.

## Requirements and implementation

| Assignment requirement | Implementation / evidence |
|---|---|
| Azure Function, every minute, three cities | `src/WeatherMonitor.Functions/FetchWeatherFunction.cs`, timer `0 */1 * * * *`; cities and country codes in the Functions `appsettings.json` |
| Full provider payload in SQL | `WeatherSnapshot.RawPayload`; EF Core SQL Server mapping and migrations in Infrastructure |
| Success/failure attempt log | `FetchAttempt`, `WeatherIngestionService` and `FetchAttemptRepository`; failed responses retain their available body with the API key redacted |
| Fetching logs UI | React Fetch log page, backed by the MVC `LogsController` |
| Chart with all cities, min/max, country, temperature and last update | React Temperature page, city cards, chart tooltip and readings table; MVC `WeatherController` |
| Date and time filtering | Presets and custom from/to date-time inputs; bounded API range queries |
| ASP.NET Core MVC, C#, TypeScript/React, EF Core, SQL | MVC API controllers and C# backend, React/TypeScript client, EF Core and SQL Server LocalDB locally |
| SOLID and unit tests | Layered dependency rules, Application ports and Infrastructure adapters; tests under `tests/` and beside React components |
| Code on GitHub | Repository linked in the root README; the reviewer must have access to the submitted branch or commit |

## Behaviour to know

- Each city is requested with its country code: London/GB, Rome/IT, Riga/LV.
- A fetch is stored every minute, even when the provider returns the same observation.
  The chart deduplicates observations; the readings table retains every successful fetch.
- Min/max values describe the provider's current city-area spread, not daily extrema.
- Stored timestamps are UTC; the UI displays the viewer's time zone with English formatting.
- A missing key prevents ingestion startup. An inactive or invalid key produces failed
  attempts rather than invented weather data. See [troubleshooting](runbooks/troubleshoot-ingestion.md).

## Checks and optional infrastructure

Use [verify changes](runbooks/verify-changes.md) for the repeatable test procedure.
.NET and front-end tests do not require an API key or SQL Server. Live operation does.

The assignment permits local Azure Functions execution. Azure hosting templates and CI/CD
are provided as additional deployment preparation; they have not yet been validated by a
deployment to an Azure subscription. Cloud deployment is not required for the local review
and creates billable resources. See [deployment](deployment.md).

No AI tool is required to run or maintain this solution. [Runbooks](runbooks/README.md),
[architecture](architecture.md), [ADRs](adr/README.md) and [security](security.md) are shared
documentation. Claude-specific files are optional development conveniences.
