# 0014. Purge history older than 90 days, daily and in batches

## Context

Every minute three attempts and three snapshots (each with ~0.5 KB of JSON) are written: about 8,600 rows and 5 MB per
day. Azure SQL Basic allows 2 GB, so without cleanup inserts would start failing after roughly a year and the fetch
log would silently stop growing.

## Decision

- A second timer function, `PurgeOldData`, runs daily at 03:30 UTC and calls `IDataRetentionService`.
- `Retention:Days` (default 90; 0 disables the purge) defines the cutoff on `FetchAttempts.AttemptedAtUtc`.
- Deletion is one set-based `ExecuteDeleteAsync` per batch of `Retention:BatchSize` (default 5,000) rows, oldest first,
  until a batch comes back short; snapshots go with their attempts through `ON DELETE CASCADE`.

## Consequences

- Storage stays around 450 MB at the default, far below the Basic limit, and query costs stay flat.
- History older than the retention period is gone; the chart range (max 31 days) is unaffected.
- Short batches keep locks and the transaction log small, so the purge never blocks the minute-by-minute writes.

## Alternatives considered

- Keep everything and move up a tier: costs grow forever for data nobody looks at.
- Null the raw payload after N days but keep the extracted columns: possible later if long-term trends are wanted.
- Table partitioning with partition switching: the right tool at much larger volumes, overkill here.
