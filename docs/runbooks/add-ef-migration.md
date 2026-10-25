# Change the data model with an EF Core migration

## 1. Change the model

- Domain entity in `src/WeatherMonitor.Domain` (`FetchAttempt`, `WeatherSnapshot`, `CurrentWeather`). Keep setters
  private; set values through the constructors/factories and enforce invariants there.
- Mapping in `src/WeatherMonitor.Infrastructure/Persistence/Configurations/*Configuration.cs`: only what the EF Core
  conventions do not give - max lengths and indexes. Every text column needs a decided length (an index key cannot
  be `nvarchar(max)`), and `DataModelContractTests` fails until the new column is added to its expected map. Nullability follows
  the C# type (`string` vs `string?`); tables, keys and the one-to-one snapshot come from conventions. Timestamps are
  `DateTime` UTC named `...Utc` (ADR 0006).
- A new payload field: extract it in `OpenWeatherMapClient.TryParse` (add it to `OpenWeatherMapResponse`) and carry it
  through `CurrentWeather` -> `WeatherSnapshot`. Decide whether a missing field fails the attempt or is nullable.

## 2. Create the migration

```bash
dotnet ef migrations add <PascalCaseName> --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure --output-dir Persistence/Migrations
```

The design-time factory (`DesignTimeDbContextFactory`) means no host is started and no connection is opened.

## 3. Review the generated files

- `Up` does exactly what you intended; nothing is dropped or renamed by accident (EF may generate drop+add for a
  rename - use `RenameColumn` by hand).
- New non-nullable columns on existing tables have a sensible default or a backfill step.
- `Down` reverses `Up`.
- `WeatherDbContextModelSnapshot.cs` changed accordingly. Never edit or delete migrations that have been applied in a
  shared environment; add a new one instead.

## 4. Apply and test

- Locally the hosts apply migrations on start (`Database:ApplyMigrationsOnStartup=true` in development), or:
  `dotnet ef database update --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure`
- Tests: extend `tests/WeatherMonitor.Infrastructure.Tests/Persistence/*` (SQLite uses `EnsureCreated` from the model, not
  migrations) and the client parse tests if a payload field was added.
- Run the `verify-changes` runbook. Then run the app and check the new column is filled (`run-locally` runbook).

## 5. Expose it (optional)

Reader projection -> DTO in `Application/<feature>` -> `src/weather-monitor.client/src/api/types.ts` -> UI. See the
`add-api-endpoint` runbook.
