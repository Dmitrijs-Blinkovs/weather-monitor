# 0015. One managed identity per app, read-only database access for the web app

- Status: Accepted (supersedes the "one shared identity" part of ADR 0011)
- Date: 2026-10-20

## Context

ADR 0011 gave both apps one user-assigned identity that was the SQL Entra administrator and could also read the API key
and write to the storage account holding the function's deployment package. The web app is public; any remote code
execution, SSRF or compromised dependency there would have inherited all of that.

## Decision

- **Function identity** (`id-func-*`): SQL Entra administrator (applies migrations), Storage Blob Data Owner (timer leases
  and Flex deployments; no queue/table roles), Key Vault Secrets User.
- **Web identity** (`id-web-*`): no Storage or Key Vault roles; in the database only `db_datareader`.
- The function grants that read access itself after migrating (`Database:ReadOnlyIdentity` settings): it creates a
  contained user from the web identity's client ID (`CREATE USER ... WITH SID = ..., TYPE = E`, which needs no directory
  permissions) and adds it to `db_datareader`. The statement is idempotent and parameterised.
- The web app no longer applies migrations (`Database__ApplyMigrationsOnStartup=false` in Azure).

## Consequences

- A compromised web app can read public weather data and nothing else: no writes, no key, no code replacement.
- The function still holds administrative database rights (needed for migrations). It is not reachable from the
  internet (inbound access denied), which limits that exposure.
- The web app cannot serve data until the function has started once after the first deployment.

## Alternatives considered

- An Entra group as SQL administrator and migrations from the pipeline (EF Core migration bundle) with separate
  `db_datareader`/`db_datawriter` users for both apps: strictest, but needs the pipeline identity inside the database and a
  T-SQL step; the natural next step for a team setup.
- Keep one identity (ADR 0011): simplest, rejected after the security review.
