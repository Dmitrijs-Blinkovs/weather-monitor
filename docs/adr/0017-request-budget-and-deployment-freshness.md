# 0017. Bound the full HTTP request and check freshness after deployment completes

## Context

The resilience handler's 30 s total timeout ends before HttpClient finishes buffering the response body. A server
that sends headers promptly but stalls its body can consume the Functions host's 55 s budget, leaving no reserve
for the independent 10 s save. Separately, a watermark recorded before deployment can accept successful fetches
from the previous Functions worker while the Web package is still being deployed.

## Decision

- Set HttpClient.Timeout and the resilience total timeout to the same budget, `OpenWeatherMap:RequestTimeout`
  (30 s; validated to 1-40 s, so fetching plus the 10 s save fit the 55 s function timeout; each attempt is capped at a third).
  Tests use a 1 s budget through the real registration, and a composition test pins the production 30 s. With the existing
  ResponseContentRead call, HttpClient covers retries and response-body buffering, including stalled bodies.
  Configure this timeout after AddStandardResilienceHandler, which otherwise disables it.
  Keep the response size cap and distinguish request timeout (failure result) from caller cancellation (propagated).
- Capture DEPLOY_COMPLETED_AT only after both azd deploy commands succeed. Require Healthy and a successful
  attempt for every shipped city whose start time is later than that watermark. Same-second timestamps are
  conservatively rejected; the next minute's timer supplies unambiguous evidence.
- Test the actual DI/resilience chain with a cancellable stalled body, and test both smoke-check decisions and
  the workflow ordering. Run the Python standard-library tests in CI without adding dependencies.

## Consequences

The fetch budget leaves room for bounded persistence. Cooperative cancellation still cannot guarantee storage if
SQL is unavailable or the process is killed. The smoke test rejects successes written during deployment, but does
not attest a specific running binary: strict release provenance would require a persisted release identifier.

## Alternatives considered

- Another custom cancellation layer: unnecessary while HttpClient already owns buffering and can bound it.
- Only a resilience timeout: does not cover body buffering outside the handler chain.
- A pre-deployment watermark: accepts old-worker results during publication.
- Persisting release IDs: stronger provenance, but unnecessary schema and application changes for this freshness fix.
