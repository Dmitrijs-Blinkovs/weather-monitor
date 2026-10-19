# 0006. UTC `DateTime` with an EF converter instead of `DateTimeOffset`

- Status: Accepted
- Date: 2026-10-19

## Context

All times come from UTC sources (provider `dt`, `TimeProvider.GetUtcNow()`). Persistence tests run on SQLite, whose EF
provider cannot order or compare `DateTimeOffset` in queries. JSON must carry an explicit zone so the browser can show
local time.

## Decision

Entities use `DateTime` properties named `...Utc`. The domain rejects `DateTimeKind.Unspecified` and converts `Local`.
A model-wide `UtcDateTimeConverter` stores UTC and restores `Kind=Utc` on read, so serialized values end in `Z`.
API query parameters are bound as `DateTimeOffset` (unambiguous input) and converted to UTC at the edge.

## Consequences

- Same model and queries run on SQL Server and SQLite.
- No time-zone arithmetic in the backend; the UI formats with the viewer's locale and zone.

## Alternatives considered

- `DateTimeOffset` everywhere: natural for SQL Server, but breaks ordering/comparison on SQLite tests and adds an
  offset that is always zero.
