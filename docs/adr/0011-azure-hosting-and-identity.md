# 0011. Azure hosting: Flex Consumption, App Service and Azure SQL

## Context

The app must run in Azure at a small cost: a timer every minute, a small web UI, a relational database. Secrets must
not live in configuration, and the setup must be reproducible by anyone (and by CI).

## Decision

- Infrastructure as code in Bicep (`infra/`), driven by the Azure Developer CLI (`azure.yaml`): `azd up` from a
  machine, `azd provision` + `azd deploy` from GitHub Actions.
- Functions on **Flex Consumption** (Linux, .NET 10 isolated): the recommended serverless plan, scale to zero, free
  monthly grant covers ~43,000 one-second runs.
- Web on **App Service Linux** (B1 by default, F1 allowed): one deployable that serves the API and the React build.
- **Azure SQL Basic**: a database written every minute never pauses, so serverless would bill continuously.
- **Managed identities instead of secrets**: SQL (Entra-only), Key Vault and Storage (identity-based
  `AzureWebJobsStorage`, shared keys disabled) are reached with user-assigned identities, one per app with least
  privilege (ADR 0015). The API key is a Key Vault secret referenced from app settings. Application Insights
  (workspace-based) for telemetry.

## Consequences

- Zero secrets in app settings, pipelines or the repository; nothing to rotate except the OpenWeatherMap key.
- Fully reproducible: `azd down --purge` and `azd up` recreate the environment.
- The function's identity is the SQL administrator so it can apply migrations on start; the web app only reads
  (ADR 0015).

## Alternatives considered

- Separate SQL administrator (a group or the deploying principal) plus contained users for the identity
  (`db_datareader`/`db_datawriter`, web read-only) and migrations from the pipeline with an EF Core migration bundle:
  stricter, needs a T-SQL step after provisioning. The natural next step when more people get access.
- SQL authentication with a password in Key Vault: simpler, but a long-lived secret.
- Container Apps for both hosts: more flexible, more moving parts for this size.
