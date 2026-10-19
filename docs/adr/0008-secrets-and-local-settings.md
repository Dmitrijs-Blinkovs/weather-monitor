# 0008. API key in user secrets; `local.settings.json` committed without secrets

- Status: Accepted
- Date: 2026-10-19

## Context

The OpenWeatherMap key must never reach source control or logs. A reviewer should be able to clone and press F5.

## Decision

- The key is read from `OpenWeatherMap:ApiKey`: user secrets locally (the Functions project has a `UserSecretsId`; Core
  Tools and the worker both load it), app settings or Key Vault in Azure. `ValidateOnStart` stops the host with a clear
  message when it is missing.
- `local.settings.json` is committed and contains only non-secret local defaults (Azurite, LocalDB, migrations on).
- The key travels in the query string, so the client's default HTTP logging is removed and every stored error message
  is redacted.

## Consequences

- Clone, set one secret, run. No secret ever appears in Git, logs or the fetch log.
- Anyone who adds a secret to `local.settings.json` must move it to user secrets instead.

## Alternatives considered

- Ignore `local.settings.json` and ship an example file: one more manual step for every new machine.
- Environment variables: easy to leak into shell history and screenshots.
