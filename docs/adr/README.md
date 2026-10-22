# Architecture decision records

Each record explains one decision: the context, what was decided, the consequences and the alternatives that were
rejected. Read the relevant record before changing something it covers. To change a decision, add a new record that
replaces the part that changed and link it from the record it changes.

| # | Decision |
|---|---|
| [0001](0001-layered-architecture.md) | Clean/onion layering with ports in Application |
| [0002](0002-raw-payload-plus-extracted-columns.md) | Store the raw payload verbatim plus extracted query columns |
| [0003](0003-log-every-attempt-isolate-failures.md) | Log every attempt; expected failures are results, not exceptions |
| [0004](0004-distinct-observations-on-read.md) | Store every minute, collapse duplicate observations on read |
| [0005](0005-cities-with-country-codes.md) | Configure cities with ISO country codes |
| [0006](0006-utc-datetime.md) | UTC `DateTime` with an EF converter instead of `DateTimeOffset` |
| [0007](0007-spa-hosted-by-aspnet-core.md) | React SPA as an `.esproj`, served by the ASP.NET Core app |
| [0008](0008-secrets-and-local-settings.md) | API key in user secrets; `local.settings.json` committed without secrets |
| [0009](0009-test-strategy.md) | Test without external dependencies |
| [0010](0010-accessible-chart.md) | Validated colour palette, colour per city, value-first tooltip |
| [0011](0011-azure-hosting-and-identity.md) | Azure hosting: Flex Consumption, App Service and Azure SQL |
| [0012](0012-ci-cd-with-github-actions.md) | CI/CD with GitHub Actions, azd and OpenID Connect |
| [0013](0013-browser-tests-with-playwright.md) | Browser tests with Playwright against the production build and a mocked API |
| [0014](0014-data-retention.md) | Purge history older than 90 days, daily and in batches |
| [0015](0015-least-privilege-identities.md) | One managed identity per app, read-only database access for the web app |
| [0016](0016-observability.md) | Observability: OpenTelemetry, ingestion metrics, data-freshness health check and an alert |
| [0017](0017-request-budget-and-deployment-freshness.md) | Full HTTP request budget and post-deployment freshness watermark |

Template for a new record (`NNNN-short-title.md`):

```markdown
# NNNN. Title

## Context
## Decision
## Consequences
## Alternatives considered
```
