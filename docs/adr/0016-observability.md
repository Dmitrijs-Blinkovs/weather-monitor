# 0016. Observability: OpenTelemetry, ingestion metrics, data-freshness health check and an alert

- Status: Accepted
- Date: 2026-10-20

## Context

Expected failures are values, not exceptions (ADR 0003), so a run in which every city fails (for example a revoked
key) is still a "successful" function invocation. Logs alone would not tell anyone that no data has arrived for an
hour. The first version also relied on Application Insights auto-instrumentation, with no traces of the outgoing
OpenWeatherMap calls from the function.

## Decision

- **OpenTelemetry in code** for both hosts, wired up and exported to Application Insights only when
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is set: `UseAzureMonitor()` in the web app; the Functions worker with
  `UseFunctionsWorkerDefaults()`, HttpClient instrumentation and `telemetryMode: OpenTelemetry` in `host.json`.
  HttpClient instrumentation redacts query-string values, so the API key never reaches telemetry.
- **Metrics** (`WeatherMonitor.Ingestion` meter): `weather.fetch.attempts{city,outcome}` and `weather.fetch.duration`.
- **Health checks:** `/health/live` (process only; App Service health probe) and `/health` (database reachable and a
  successful fetch within 10 minutes - Degraded otherwise). The deployment smoke test requires `Healthy`, which proves
  the whole chain timer -> provider -> database -> web.
- **Alert** (Bicep): a scheduled log query on the metric fires when no success was recorded for 15 minutes; e-mail via an
  optional action group (`ALERT_EMAIL`).

## Consequences

- An on-call engineer sees per-city success rates and latencies, distributed traces of each run, and gets alerted when
  data stops arriving - including the case where everything "succeeds" technically.
- Locally nothing is exported and OpenTelemetry is not wired up. This matters for the function: with
  `UseFunctionsWorkerDefaults()` the worker reports its own telemetry and the host stops forwarding worker logs, so
  without an exporter the console would show no application log at all (found in the live run of 2026-10-20).

## Amendment (2026-10-20)

- `weather.fetch.attempts` and `weather.fetch.duration` are recorded only after the run is stored. When the database
  rejects the writes (down, full, schema mismatch), no success is counted, so the alert fires; the service also logs
  "Storing N fetch attempts failed" as an error before the invocation fails.

## Alternatives considered

- Classic Application Insights SDK: its dependency collector records full URLs, which would include the API key.
- Alerting on log text: brittle against message changes; metrics are the contract.
