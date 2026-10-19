# Architecture decision records

Each record explains one decision: the context, what was decided, the consequences and the alternatives that were
rejected. Read the relevant record before changing something it covers. To change a decision, add a new record that
supersedes the old one (and mark the old one `Superseded by NNNN`) instead of rewriting history.

| # | Decision | Status |
|---|---|---|
| [0001](0001-layered-architecture.md) | Clean/onion layering with ports in Application | Accepted |
| [0002](0002-raw-payload-plus-extracted-columns.md) | Store the raw payload verbatim plus extracted query columns | Accepted |
| [0003](0003-log-every-attempt-isolate-failures.md) | Log every attempt; expected failures are results, not exceptions | Accepted |
| [0004](0004-distinct-observations-on-read.md) | Store every minute, collapse duplicate observations on read | Accepted |
| [0005](0005-cities-with-country-codes.md) | Configure cities with ISO country codes | Accepted |
| [0006](0006-utc-datetime.md) | UTC `DateTime` with an EF converter instead of `DateTimeOffset` | Accepted |
| [0007](0007-spa-hosted-by-aspnet-core.md) | React SPA as an `.esproj`, served by the ASP.NET Core app | Accepted |
| [0008](0008-secrets-and-local-settings.md) | API key in user secrets; `local.settings.json` committed without secrets | Accepted |
| [0009](0009-test-strategy.md) | Test without external dependencies (SQLite, WebApplicationFactory, MTP) | Accepted |
| [0010](0010-accessible-chart.md) | Validated colour palette, colour per city, value-first tooltip | Accepted |

Template for a new record (`NNNN-short-title.md`):

```markdown
# NNNN. Title

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
## Decision
## Consequences
## Alternatives considered
```
