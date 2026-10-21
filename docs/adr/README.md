# Architecture decision records

Each record explains one decision: the context, what was decided, the consequences and the alternatives that were
rejected. Read the relevant record before changing something it covers. To change a decision, add a new record that
supersedes the old one (and mark the old one `Superseded by NNNN`, or `Accepted (partly superseded by NNNN)`) instead
of rewriting history; a record may also carry a dated amendment.

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
| [0009](0009-test-strategy.md) | Test without external dependencies | Accepted |
| [0010](0010-accessible-chart.md) | Validated colour palette, colour per city, value-first tooltip | Accepted |
| [0011](0011-azure-hosting-and-identity.md) | Azure hosting: Flex Consumption, App Service, Azure SQL and one managed identity | Accepted (identity part superseded by 0015) |
| [0012](0012-ci-cd-with-github-actions.md) | CI/CD with GitHub Actions, azd and OpenID Connect | Accepted |
| [0013](0013-browser-tests-with-playwright.md) | Browser tests with Playwright against the production build and a mocked API | Accepted |
| [0014](0014-data-retention.md) | Purge history older than 90 days, daily and in batches | Accepted |
| [0015](0015-least-privilege-identities.md) | One managed identity per app, read-only database access for the web app | Accepted (supersedes part of 0011) |
| [0016](0016-observability.md) | Observability: OpenTelemetry, ingestion metrics, data-freshness health check and an alert | Accepted |

Template for a new record (`NNNN-short-title.md`):

```markdown
# NNNN. Title

- Status: Proposed | Accepted | Accepted (supersedes NNNN) | Accepted (partly superseded by NNNN) | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
## Decision
## Consequences
## Alternatives considered
```
