# 0003. Log every attempt; expected failures are results, not exceptions

## Context

The assignment requires a log of successful and failed attempts. Three independent cities are fetched per run; one bad
city (or a transient outage) must not hide the others.

## Decision

- `IWeatherProvider` returns `WeatherFetchResult.Success/Failure` for every expected outcome (HTTP error, timeout,
  network error, invalid payload). Only bugs throw; the ingestion service catches those too as a safety net.
- Each city becomes one `FetchAttempt` (status code, error, duration). All attempts of a run are saved in one
  `SaveChanges` (one transaction). Cities are fetched in parallel.
- Transient errors are retried by `Microsoft.Extensions.Http.Resilience` within a 30 s budget, shorter than the
  one-minute schedule.

## Consequences

- The log answers "what happened at 10:17 for Rome?" for every minute, including why it failed.
- A database outage fails the whole invocation (nothing can be logged then); the Functions runtime records it and the
  next run proceeds normally.

## Alternatives considered

- Exceptions for HTTP failures: callers would have to know each implementation's exceptions, which the interface
  does not describe (so implementations would not be interchangeable), and partial success becomes awkward.
- Saving each city separately: partial runs on crash, three round trips instead of one.
