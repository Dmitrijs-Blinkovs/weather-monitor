---
name: deploy-to-azure
description: Provision and deploy Weather Monitor to Azure with the Azure Developer CLI and Bicep, set up GitHub Actions continuous deployment with OIDC, or change the infrastructure templates. Use when asked to deploy, host in Azure, change infra/, azure.yaml or the deploy workflow, or tear an environment down. Never acts on a subscription without the user's explicit go-ahead.
---

# Deploy to Azure

Reference: `docs/deployment.md` (what gets created, costs, security model, operations, hardening), `docs/security.md`
and ADRs 0011, 0012, 0015, 0016.

## Ground rules (stop if one is not met)

- Deploying creates billable resources: get the user's explicit approval for the subscription, region, environment name
  and web SKU before running anything that talks to Azure (`azd provision/up/deploy/down`, `az ... create`).
- Never use a subscription just because it is the CLI default; set it explicitly (`azd env set AZURE_SUBSCRIPTION_ID`).
- Never read, print or ask for the OpenWeatherMap key; the user sets it (`azd env set OPENWEATHERMAP_API_KEY ...` or the
  `production` environment secret on GitHub).
- `azd down --purge` deletes all data; only with explicit consent.

## Change the templates (safe, local only)

1. Edit `infra/resources.bicep` (resources) or `infra/main.bicep` (parameters/outputs); keep secrets out of outputs.
2. Keep least privilege: the function identity (`id-func-*`) is the SQL admin and the only one with Key Vault and
   Storage roles; the web identity (`id-web-*`) only reads the database. Give a new permission to the one app that
   needs it. App settings use managed identities (no keys/passwords); new secrets go to Key Vault with a reference.
3. Validate: `az bicep build --file infra/main.bicep --stdout > /dev/null` - no warnings (CI enforces this).
4. Update `docs/deployment.md`, `docs/security.md` (accepted risks) and the ADR if a decision changed.

## Change the deploy workflow

- The build job must stay credential-free (no `id-token`, no secrets); only the deploy job logs in and it must not run
  `npm`, `dotnet build/publish` or any other build tooling - it deploys the build job's packages (`--from-package`).
- Keep the `workflow_run` guard (`event == 'push'` and `head_repository == github.repository`): the branch filter alone
  matches a fork's branch named `main`.
- Pin new actions to a full commit SHA with the version as a comment; Dependabot keeps them current.

## First deployment (user present)

```bash
azd auth login
azd env new <env-name>                       # e.g. weather-prod
azd env set AZURE_SUBSCRIPTION_ID <id>       # confirmed with the user
azd env set AZURE_LOCATION <region>          # e.g. northeurope
azd env set ALERT_EMAIL <address>            # optional, for the "no weather data" alert
# user runs: azd env set OPENWEATHERMAP_API_KEY <key>
azd up
```

Check region support first: `az functionapp list-flexconsumption-locations --runtime dotnet-isolated --runtime-version 10.0`.

Verify:
- `<WEB_URL>/health/live` is `Healthy`; `<WEB_URL>/health` is `Healthy` within a few minutes (`Degraded` = no successful
  fetch yet - see `troubleshoot-ingestion`; `Unhealthy`/503 = the web app cannot reach the database).
- The fetch log shows 3 successful attempts per minute; Application Insights shows the `weather.fetch.attempts` metric.
- Function fails at start with `OpenWeatherMap:ApiKey is not configured` -> the Key Vault reference did not resolve:
  check `id-func-*`'s Key Vault role and `keyVaultReferenceIdentity`.
- Web app gets `Login failed for user '<token-identified principal>'` -> the web identity has no database user yet: the
  function creates it after migrating, so the function has not started, or `Database__ReadOnlyIdentity__*` on the
  function app is wrong.

## Continuous deployment

1. `azd pipeline config` (creates the app registration, federated credentials and repository variables).
2. In Entra ID keep a single federated credential with subject `repo:<owner>/<repo>:environment:production`; delete the
   branch and `pull_request` ones. Prefer role assignments scoped to the resource group.
3. Settings > Environments > `production`: required reviewers, deployments from `main` only, environment secret
   `OPENWEATHERMAP_API_KEY` (delete a repository secret of that name if azd created one). Optional variables
   `WEB_SKU_NAME`, `ALERT_EMAIL`.
4. From then on every green CI run on `main` deploys after approval; **Deploy to Azure** can also be started manually
   from `main`. The smoke test requires `/health` to report `Healthy` within 5 minutes.
