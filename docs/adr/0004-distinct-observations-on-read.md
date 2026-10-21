# 0004. Store every minute, collapse duplicate observations on read

- Status: Accepted
- Date: 2026-10-19

## Context

The assignment says to fetch every minute. OpenWeatherMap refreshes an observation (its `dt`, the "last update time")
roughly every 10 minutes, so most fetches return an observation already stored. Charting every fetch would draw
plateaus and multiply the point count by ~10.

## Decision

Keep writing every fetch (requirement). The history reader selects `DISTINCT` over the observation columns
(city, country, temperatures, `ObservedAtUtc`) and the chart's time axis is the observation time.

## Consequences

- Each point on the chart is a real observation; the x position is the provider's update time shown in the UI.
- 7 days are about 1,000 points per city instead of 10,000; a 31-day cap bounds the response.
- Storage grows by ~4,300 attempts plus ~4,300 snapshots a day; a daily purge keeps 90 days (ADR 0014).

## Amendment (2026-10-21): the table lists every fetch

With only distinct observations on screen, a city looked frozen for 10-20 minutes although a fetch arrived every
minute, which read as a broken pipeline. The chart keeps the distinct observations; the table view of the Temperature
page now lists every successful fetch (`GET /api/weather/readings`, server-paged) with both the fetch time and the
provider's last update time, so a repeated observation is visibly a new response with the same data. Failed fetches
stay on the fetch log page.

## Alternatives considered

- Skip storing when `dt` has not changed: contradicts "store the full payload" every minute and hides fetch health.
- Bucket by time on the server (e.g. `DATEDIFF` buckets): provider-specific SQL that SQLite tests cannot run; not
  needed at this volume.
