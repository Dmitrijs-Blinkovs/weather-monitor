# Performance and load test

How the system behaves after running for a long time: a month (the longest range the UI and API accept) and 90 days
(the retention limit, so the largest tables the app ever keeps), with every request a real database round trip,
under concurrent load, and while the retention purge removes a large backlog.

## Result in short

| Question | Answer (90 days, 388,800 attempts, 648 MB) |
|---|---|
| Is every page fast at the largest volume? | Yes. Every API request answers in under 100 ms uncached (the 31-day chart, the heaviest, in ~95 ms). |
| Does paging stay flat as the tables grow? | Yes. Fetch-log page 1,000 x 200 (offset 199,800) takes 32 ms; readings page 1,000 of a month 14 ms. |
| Is the payload to the browser bounded? | Yes. The longest chart range is 13,389 points: 249 KB gzip; the UI downsamples to 600 points per city. |
| Does the UI stay responsive with a month of data? | Yes. Production build: ~0.4 s of main-thread work to draw 31 days, no long task while moving the tooltip. |
| What happens under more load than it accepts? | 64 concurrent requests all succeed (103 requests/s). At 256 concurrent, 192 are served (64 running + 128 queued) and the rest get a clean `429`; no errors, no timeouts. |
| Does the retention purge fit the function timeout (55 s)? | Yes. A day's purge (4,341 attempts) takes 0.1 s; a backlog of 254,880 attempts (retention cut from 90 to 30 days) 9.9 s in 51 batches. |

Nothing had to change in the code: the design decisions that make this hold are listed at the end.

## How to run it

Everything runs locally against a **separate database** (`WeatherMonitorLoadTest` in the same LocalDB instance), so the
development data in `WeatherMonitor` is never touched; the scripts refuse any database name without `LoadTest`.

```powershell
./tools/load-test/Seed-LoadTestData.ps1 -Days 90     # recreate the database and fill 90 days (about 20 s plus the EF build)
./tools/load-test/Start-LoadTestApi.ps1              # second API on http://127.0.0.1:5280 (keeps running; Ctrl+C stops it)
./tools/load-test/Measure-Api.ps1 -Clients 64        # in another terminal: the report below, as Markdown
./tools/load-test/Measure-Purge.ps1 -RetentionDays 30  # deletes data in the load-test database: time the purge
```

- **Seeding** recreates the schema with the real EF Core migrations, then inserts day by day one attempt per city per
  minute: about 0.5% failures (a 503 with an HTML body, or a timeout without one), and for every success a snapshot
  with an OpenWeatherMap-shaped payload (~0.7 KB). As with the real provider, a city's observation ("last update")
  changes only every 10 minutes, at its own minute, so the history reader really has to collapse duplicates.
- **The API** is a Release build in a temporary folder (a running Visual Studio session keeps its own), without the SPA
  proxy and with the per-client rate limit raised, so the measurement hits the API rather than the limiter. The global
  concurrency limit (64 + a queue of 128) stays as in production.
- **Measuring** adds a unique query parameter to every request, so the output cache never answers; each number is the
  median and the slowest of 10 uncached runs after one warm-up. One cached request is measured separately.
- **The UI** was checked in the browser with the production build (`vite preview`, whose proxy forwards `/api` to the
  load-test API), recording the main thread's long tasks with a `PerformanceObserver`.

## Results

Measured on 21 Oct 2026 on a developer laptop: SQL Server LocalDB, API and database on the same machine, Windows
PowerShell as the client. Absolute numbers depend on the machine; the growth between 31 and 90 days is what matters.

### API at 90 days (388,800 attempts, 386,840 snapshots, 648 MB data file)

| Request | Median ms | Slowest ms | On the wire (gzip) | Uncompressed | Rows |
|---|---:|---:|---:|---:|---|
| Liveness /health/live | 1 | 1 | 0.0 KB | 0.0 KB | Healthy |
| Readiness /health (database) | 3 | 4 | 0.0 KB | 0.0 KB | Healthy |
| Cities | 33 | 44 | 0.0 KB | 0.0 KB | 3 |
| History, last hour | 1 | 3 | 0.6 KB | 2.2 KB | 15 points |
| History, last 6 hours | 2 | 3 | 2.3 KB | 12.2 KB | 105 points |
| History, last 24 hours | 5 | 7 | 8.7 KB | 48.0 KB | 429 points |
| History, last 7 days | 22 | 33 | 57.1 KB | 336.1 KB | 3,021 points |
| History, 31 days (maximum) | 95 | 110 | 248.6 KB | 1,486.9 KB | 13,389 points |
| Readings, 24 hours, page 1 | 3 | 4 | 1.0 KB | 11.7 KB | 60 of 4,274 |
| Readings, 31 days, page 1 | 9 | 11 | 1.0 KB | 11.7 KB | 60 of 133,228 |
| Readings, 31 days, page 1,000 | 14 | 16 | 0.9 KB | 11.5 KB | 60 of 133,228 |
| Fetch log, page 1 | 20 | 26 | 0.5 KB | 3.1 KB | 25 of 388,800 |
| Fetch log, page 1,000 x 200 | 32 | 34 | 2.2 KB | 24.2 KB | 200 of 388,800 |
| Fetch log, failures of one city | 14 | 46 | 0.6 KB | 3.1 KB | 25 of 639 |
| Attempt details (payload) | 1 | 2 | 0.6 KB | 0.7 KB | |

- The same 31-day history request answered from the output cache: 13 ms.
- At 31 days (133,920 attempts, 264 MB) the numbers were the same or lower except the counts: fetch-log page 1 15 ms,
  cities 25 ms. Requests bounded by a time range (history, readings) do not grow with the table at all.

### Concurrency (90 days)

| Load | Result |
|---|---|
| 5 rounds of 64 concurrent uncached requests (7- and 31-day history, readings, log page 1 and page 1,000) | 320 x `200`, 103 requests/s |
| 3 rounds of 256 concurrent uncached requests | 576 x `200`, 192 x `429` (64 per round over the 64 + 128 limit), 148 requests/s; no `5xx`, no timeouts |

### Browser (production build, 31 days in the database)

| Action | Main-thread long tasks |
|---|---|
| Last 7 days | 64 ms + 137 ms |
| Custom range of 31 days | 182 ms + 200 ms |
| Moving the tooltip across 31 days | none |

The development build (`npm run dev`: unminified React, StrictMode renders twice) needs about three times as long
(593 + 709 ms for 31 days); judge performance on the production build.

### Retention purge (from the 90-day database)

| Purge | Rows | Batches of 5,000 | Time | Slowest batch |
|---|---:|---:|---:|---:|
| Daily (retention 89 days: one day falls out) | 4,341 | 1 | 0.1 s | 116 ms |
| Backlog (retention cut from 90 to 30 days) | 254,880 | 51 | 9.9 s | 383 ms |

## Why it holds, and where the limits are

- **Bounded requests.** Every range is capped at 31 days and every page at 200 rows and page 1,000
  (`WeatherHistoryRules`, `PageLimits`); retention caps the tables at 90 days. No request can grow with time.
- **Indexes that match the queries.** The chart reads a covering index on `(ObservedAtUtc, City)` and never touches
  the payload column; the log and the readings page over `(IsSuccess, AttemptedAtUtc)` / `(AttemptedAtUtc)` with a
  deferred join, so a deep page costs index keys, not rows.
- **Less data to the browser.** Duplicate observations are collapsed on read (13,389 points for a month instead of
  133,000 fetches), responses are compressed, and the chart downsamples each city to 600 points.
- **Overload is refused, not suffered.** The concurrency limiter answers `429` beyond 64 running and 128 queued
  requests, and the output cache absorbs repeated polling.
- **Counts and the city list grow with the table.** `COUNT(*)` for the log total and `DISTINCT City` scan an index:
  33 ms at 90 days, linear in the row count, cached for 10 s and 5 min. Fine at the retention limit; a much longer
  retention would call for a city table or a cached count.
- **Storage fits the planned database.** 90 days take a 648 MB data file (allocated, including free space), within
  the 2 GB of the Azure SQL Basic tier in [deployment.md](deployment.md).
- **Not covered here:** Azure SQL itself. Basic (5 DTU) has far less CPU and I/O than a laptop, so expect the uncached
  numbers to be several times higher there; the queries and plans are the same, so the relative picture holds, the
  output cache still absorbs polling, and the SQL tier is the first scaling knob ([deployment.md](deployment.md)).
