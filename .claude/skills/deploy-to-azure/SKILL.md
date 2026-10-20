---
name: deploy-to-azure
description: Provision and deploy Weather Monitor to Azure with the Azure Developer CLI and Bicep, set up GitHub Actions continuous deployment with OIDC, or change the infrastructure templates. Use when asked to deploy, host in Azure, change infra/, azure.yaml or the deploy workflow, or tear an environment down. Never acts on a subscription without the user's explicit go-ahead.
---

# Deploy to Azure

Reference: `docs/deployment.md` (what gets created, costs, security model) and ADR 0011/0012.

## Ground rules (stop if one is not met)

- Deploying creates billable resources: get the user's explicit approval for the subscription, region, environment name
  and web SKU before running anything that talks to Azure (`azd provision/up/deploy/down`, `az ... create`).
- Never use a subscription just because it is the CLI default; set it explicitly (`azd env set AZURE_SUBSCRIPTION_ID`).
- Never read, print or ask for the OpenWeatherMap key; the user sets it (`azd env set OPENWEATHERMAP_API_KEY ...` or the
  GitHub secret).
- `azd down --purge` deletes all data; only with explicit consent.

## Change the templates (safe, local only)

1. Edit `infra/resources.bicep` (resources) or `infra/main.bicep` (parameters/outputs); keep secrets out of outputs.
2. App settings must use the managed identity (no keys/passwords); new secrets go to Key Vault with a reference.
3. Validate: `az bicep build --file infra/main.bicep --stdout > /dev/null` - no warnings (CI enforces this).
4. Update `docs/deployment.md` (and the ADR if a decision changed).

## First deployment (user present)

```bash
azd auth login
azd env new <env-name>                       # e.g. weather-prod
azd env set AZURE_SUBSCRIPTION_ID <id>       # confirmed with the user
azd env set AZURE_LOCATION <region>          # e.g. northeurope
# user runs: azd env set OPENWEATHERMAP_API_KEY <key>
azd up
```

Check region support first: `az functionapp list-flexconsumption-locations --runtime dotnet-isolated --runtime-version 10.0`.

Verify: `<WEB_URL>/health` is `Healthy`; the fetch log shows 3 successful attempts per minute; Application Insights
shows `FetchWeather` invocations. If the function fails at start with `OpenWeatherMap:ApiKey is not configured`, the
Key Vault reference did not resolve - check the identity's Key Vault role and `keyVaultReferenceIdentity`.

## Continuous deployment

1. `azd pipeline config` (creates the federated credential and repository variables).
2. User adds repository secret `OPENWEATHERMAP_API_KEY`; optionally variable `WEB_SKU_NAME`.
3. Protect the `production` environment with required reviewers.
4. Run **Deploy to Azure** from the Actions tab. Add `push: branches: [main]` only when the user wants automatic deploys.
