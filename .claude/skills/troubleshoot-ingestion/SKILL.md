---
name: troubleshoot-ingestion
description: Diagnose why Weather Monitor is not storing weather data - failed attempts in the fetch log, no new rows, wrong city or country, timer not firing, host not starting. Use when the fetch log shows failures, the chart has no new points, or the Functions host misbehaves.
---

# Troubleshoot ingestion

Work from evidence: the fetch log (UI or `FetchAttempts` table) and the Functions host output. Never print the API key.

## 1. Is the host running and the timer firing?

| Host output | Meaning / action |
|---|---|
| `OptionsValidationException ... OpenWeatherMap:ApiKey is not configured` | Key missing for the Functions project. Ask the user to set it (user secrets); check presence only. |
| `OptionsValidationException ... Weather` / `Cities` | Invalid city list in `appsettings.json` (see `manage-cities`). |
| Storage / `127.0.0.1:10000` connection errors, timer never fires | Azurite is not running. |
| No `Executing 'Functions.FetchWeather'` lines | Host not started, or stuck at startup - read the first error in the output. |
| `Weather ingestion completed: N succeeded, M failed` | Host is fine; continue with the failed attempts. |

Trigger a run on demand: `POST http://localhost:7071/admin/functions/FetchWeather` with body `{"input":""}`.

## 2. Read the failed attempts

```sql
SELECT TOP 20 AttemptedAtUtc, City, HttpStatusCode, ErrorMessage, DurationMs
FROM FetchAttempts WHERE IsSuccess = 0 ORDER BY Id DESC;
```

| HttpStatusCode / error | Cause | Action |
|---|---|---|
| 401 `Invalid API key` | key not activated yet (up to ~2 h) or wrong | wait / user re-sets the key |
| 404 `city not found` | misspelt `Name` or wrong `CountryCode` | fix `Weather:Cities` |
| 429 | rate limit (60/min on free plan) shared with another app | reduce load, wait |
| 5xx, `Request timed out`, circuit open | provider trouble; resilience already retried | transient - confirm the next minutes recover |
| 200 + `missing one of the required fields` / `not valid JSON` | provider response changed | inspect a raw payload, update `OpenWeatherMapResponse` / `TryParse` and its tests |
| null + `Request failed: ...` | DNS/proxy/firewall | check network from the host |

## 3. Data looks wrong

- Wrong country (e.g. Rome `US`): city without `CountryCode` (ADR 0005). Fix config; old rows keep the wrong
  country - offer the user to delete them or reset the local database (`docs/getting-started.md`, section 9). Never
  delete data without the user's explicit consent.
- Inspect a payload: `SELECT TOP 1 RawPayload FROM WeatherSnapshots WHERE City = 'Rome' ORDER BY Id DESC`
  (`JSON_VALUE(RawPayload, '$.coord.lat')` etc.).
- Chart "not updating": expected between provider updates (~10 min); compare `ObservedAtUtc` with `FetchedAtUtc`.

## 4. Fix and prove

Reproduce in a unit test where possible (`OpenWeatherMapClientTests` with a recorded body, or
`WeatherIngestionServiceTests`), fix, run `verify-changes`, then watch two or three live runs (`run-locally`).
