# 0011. Azure hosting: Flex Consumption, App Service, Azure SQL and one managed identity

- Status: Accepted (not deployed yet); the shared-identity part is superseded by ADR 0015
- Date: 2026-10-19

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
- **One user-assigned managed identity** for both apps: SQL (Entra-only), Key Vault (secret reader) and Storage
  (identity-based `AzureWebJobsStorage`, shared keys disabled). The API key is a Key Vault secret referenced from app
  settings. Application Insights (workspace-based) for telemetry.
- The identity is the SQL server's Entra **administrator**, so the hosts can apply EF Core migrations on start without
  a post-provisioning script.

## Consequences

- Zero secrets in app settings, pipelines or the repository; nothing to rotate except the OpenWeatherMap key.
- Fully reproducible: `azd down --purge` and `azd up` recreate the environment.
- The apps have more database rights than they need at run time (least privilege is relaxed for simplicity).

## Alternatives considered

- Separate SQL administrator (a group or the deploying principal) plus contained users for the identity
  (`db_datareader`/`db_datawriter`, web read-only) and migrations from the pipeline with an EF Core migration bundle:
  stricter, needs a T-SQL step after provisioning. The natural next step when more people get access.
- SQL authentication with a password in Key Vault: simpler, but a long-lived secret.
- Container Apps for both hosts: more flexible, more moving parts for this size.
