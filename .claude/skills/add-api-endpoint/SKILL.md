---
name: add-api-endpoint
description: Add or change a read endpoint end to end in Weather Monitor - Application port and DTO, EF Core reader, MVC controller with validated request model, tests, and the typed client and UI in React. Use for any new data the UI should show or any change to an /api route.
---

# Add an API endpoint (and show it in the UI)

Follow the existing slices as templates: `FetchLog` (paged list) and `History` (computed view).

## 1. Application (contract first)

In `src/WeatherMonitor.Application/<Feature>/`:
- DTOs as `sealed record`s; timestamps `DateTime` UTC with a `...Utc` suffix.
- A read port `I<Feature>Reader` (what storage must answer). If there is logic beyond a query (grouping, summaries,
  validation), add an `I<Feature>Service` + `internal sealed` implementation, register it in
  `Application/DependencyInjection.cs`, and keep shared rules in a static `*Rules` class (see `WeatherHistoryRules`).

## 2. Infrastructure

`src/WeatherMonitor.Infrastructure/Persistence/<Feature>Reader.cs` (`internal sealed`): `AsNoTracking()`, project
straight to DTOs or anonymous types, never load `RawPayload` unless needed, keep queries translatable by both SQL Server
and SQLite. Register it in `Infrastructure/DependencyInjection.cs` (`AddPersistence`).

## 3. Web

- Request model in `src/WeatherMonitor.Web/Models/` with DataAnnotations; cross-field rules via `IValidatableObject`.
  Bind times as `DateTimeOffset` and convert with `.UtcDateTime`.
- Controller in `Controllers/`: `[ApiController]`, `[Route("api/<name>")]`, `[ProducesResponseType<T>(200)]` and
  `[ProducesResponseType<ValidationProblemDetails>(400)]`. Do NOT add `[Produces("application/json")]` (it breaks
  problem+json). The action only maps the request and delegates.
- Add example requests to `src/WeatherMonitor.Web/WeatherMonitor.Web.http`.

## 4. Tests

- Application: service logic with substitutes (`tests/WeatherMonitor.Application.Tests`).
- Infrastructure: reader against `SqliteTestDatabase` - filtering, ordering, paging, UTC.
- Web: `WeatherApiFactory` (substitute the port) - 200 JSON shape (camelCase, `Z` timestamps, enums as strings),
  each validation rule returns 400 `application/problem+json`, and the service is not called on invalid input.

## 5. Front end

- Types in `src/weather-monitor.client/src/api/types.ts`; a method on `weatherApi` in `api/weatherApi.ts` (use
  `getJson`, pass the `AbortSignal`).
- Use it through `useWeatherApi()` + `useQuery` in the feature; add the method to `fakeApi()` in `src/test/fixtures.ts`.
- Component tests with `renderWithProviders(<Page />, fakeApi({...}))`.
- Charts: follow ADR 0010 - colours only via `colorForCity` / CSS tokens, legend plus direct labels, a table view.

## 6. Finish

Update the README API table and `docs/architecture.md` section 6, then run the `verify-changes` skill and look at the
result in the running app (`run-locally`).
