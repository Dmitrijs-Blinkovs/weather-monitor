# 0005. Configure cities with ISO country codes

- Status: Accepted
- Date: 2026-10-19

## Context

During the first local run, `q=Rome` returned Rome, Georgia, USA (lat 34.26, lon -85.16, country `US`), not Rome, Italy.
City names alone are ambiguous for the provider.

## Decision

`Weather:Cities` is a list of `{ "Name": "Rome", "CountryCode": "IT" }`. The provider is called with
`q={Name},{CountryCode}` (the documented disambiguation form). The plain `Name` is the key used in the log, the API and
the UI. `CountryCode` is optional but validated as two letters when present.

## Consequences

- The stored data is for the intended city; the chart and log keep showing just "Rome".
- Rows stored before the fix keep `Country = US` - clean them up (or recreate a local database) if they exist.

## Alternatives considered

- City IDs (`id=3169070`): unambiguous but unreadable in configuration and logs.
- Coordinates (`lat`/`lon`): precise, but the assignment names cities and the provider's name/country would still vary.
