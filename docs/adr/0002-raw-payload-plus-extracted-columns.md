# 0002. Store the raw payload verbatim plus extracted query columns

- Status: Accepted
- Date: 2026-10-19

## Context

The assignment requires storing the full payload. The UI needs city, country, temperature, min, max and the last update
time, filtered by time range, for every city.

## Decision

`WeatherSnapshots.RawPayload` (`nvarchar(max)`) keeps the response body exactly as received. On write, the fields the UI
needs are extracted once into typed columns, with a covering index `(ObservedAtUtc, City) INCLUDE (...)`.

## Consequences

- Chart queries never parse JSON and never read the large column.
- The payload can be re-processed later (for example with `JSON_VALUE`) if more fields are needed.
- Extraction logic lives in one place (`OpenWeatherMapClient.TryParse`); a response missing required fields is logged as
  a failed attempt instead of storing a half-filled snapshot.

## Alternatives considered

- JSON column only, query with `JSON_VALUE`/computed columns: slower, provider-specific, harder to test on SQLite.
- Extracted columns only: violates the requirement and loses data.
- SQL Server 2025 native `json` type: not available on LocalDB, so it would break the local setup.
