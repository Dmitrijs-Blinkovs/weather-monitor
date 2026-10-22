# 0015. One managed identity per app, read-only database access for the web app

## Context

One identity shared by both apps would be the SQL Entra administrator, read the API key and write to the storage
account holding the function's deployment package. The web app is public; any remote code execution, SSRF or
compromised dependency there would inherit all of that.

## Decision

- **Function identity** (`id-func-*`): SQL Entra administrator (applies migrations), Storage Blob Data Owner (timer leases
  and Flex deployments; no queue/table roles), Key Vault Secrets User.
- **Web identity** (`id-web-*`): no Storage or Key Vault roles; in the database only `db_datareader`.
- The function grants that read access itself after migrating (`Database:ReadOnlyIdentity` settings): it creates a
  contained user from the web identity's client ID (`CREATE USER ... WITH SID = ..., TYPE = E`, which needs no directory
  permissions) and adds it to `db_datareader`. The statement is idempotent and parameterised.
- The web app does not apply migrations (`Database__ApplyMigrationsOnStartup=false` in Azure).

## Consequences

- A compromised web app can read public weather data and nothing else: no writes, no key, no code replacement.
- The function still holds administrative database rights (needed for migrations). It is not reachable from the
  internet (inbound access denied), which limits that exposure.
- The web app cannot serve data until the function has started once after the first deployment.

## Alternatives considered

- An Entra group as SQL administrator and migrations from the pipeline (EF Core migration bundle) with separate
  `db_datareader`/`db_datawriter` users for both apps: strictest, but needs the pipeline identity inside the database and a
  T-SQL step; the natural next step for a team setup.
- One identity shared by both apps: simplest, but the public web app would hold every right the function needs.
