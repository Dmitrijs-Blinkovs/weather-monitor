# Deploying to Azure

Status: the infrastructure templates (`infra/`), the azd definition (`azure.yaml`) and the deployment workflow
(`.github/workflows/deploy.yml`) are ready and compile, but nothing has been deployed yet. The first deployment is a
deliberate, manual step.

## What gets created

One resource group `rg-<environment>` with:

| Resource | SKU | Purpose | Approx. cost / month |
|---|---|---|---|
| User-assigned managed identity | - | the only identity the apps use (SQL, Key Vault, Storage) | free |
| Function app (Linux) | Flex Consumption FC1, 512 MB | `FetchWeather` timer, every minute | ~0 (inside the monthly free grant) |
| Storage account | Standard LRS, shared keys disabled | timer leases, Flex deployment packages | cents |
| App Service plan + web app (Linux) | B1 (default) or F1 | MVC API + React UI, health check `/health` | ~13 USD (B1) / 0 (F1) |
| Azure SQL server + database `weatherdb` | Basic (5 DTU, 2 GB) | fetch log and snapshots | ~5 USD |
| Key Vault | Standard, RBAC | the OpenWeatherMap API key | cents |
| Log Analytics + Application Insights | pay-as-you-go | logs, requests, function invocations | free up to 5 GB/month |

Why Basic SQL and not serverless: a database written every minute never auto-pauses, so the serverless free offer
(100,000 vCore-seconds) would be used up within days.

## Security model

- No passwords anywhere: the apps authenticate to SQL (Entra-only authentication), Key Vault and Storage with one
  user-assigned managed identity. Storage shared keys are disabled; SQL accepts only Entra ID.
- The API key is a Key Vault secret; the function's app setting is a Key Vault reference.
- HTTPS only, TLS 1.2 minimum, FTP disabled, SQL reachable only from Azure services.
- Trade-off: the apps' identity is the SQL server's Entra administrator so it can apply EF Core migrations on start.
  See [ADR 0011](adr/0011-azure-hosting-and-identity.md) for the stricter alternative.

## Prerequisites

- An Azure subscription where you have **Owner** (or Contributor + User Access Administrator): the templates create
  role assignments. Choose the subscription deliberately - azd uses the one you set, not your CLI default.
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd):
  `winget install Microsoft.Azd`
- .NET 10 SDK and Node.js 22 (the web publish builds the React app).
- Flex Consumption with .NET 10 must be available in the region. Check with
  `az functionapp list-flexconsumption-locations --runtime dotnet-isolated --runtime-version 10.0`.

## First deployment from your machine

```bash
azd auth login
```

```bash
azd env new weather-prod
```

```bash
azd env set AZURE_SUBSCRIPTION_ID <subscription-id>
```

```bash
azd env set AZURE_LOCATION northeurope
```

```bash
azd env set OPENWEATHERMAP_API_KEY <your-api-key>
```

Optional, free web tier (slower first request, no health check):

```bash
azd env set WEB_SKU_NAME F1
```

```bash
azd up
```

`azd up` provisions `infra/main.bicep` and deploys both services (`azure.yaml`). It prints `WEB_URL` at the end.
The azd environment (including the key) is stored under `.azure/`, which is ignored by Git.

Verify:
1. `<WEB_URL>/health` returns `Healthy`.
2. Within a minute the Fetch log shows three successful attempts.
3. Application Insights > Transaction search shows `FetchWeather` invocations every minute.

## Continuous deployment from GitHub

1. Connect the repository to Azure once (creates an app registration with a federated credential for GitHub OIDC,
   assigns it roles and sets the repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
   `AZURE_ENV_NAME`, `AZURE_LOCATION`):
   ```bash
   azd pipeline config
   ```
2. Add the repository secret `OPENWEATHERMAP_API_KEY` (Settings > Secrets and variables > Actions).
   Optionally add the variable `WEB_SKU_NAME`.
3. Settings > Environments > `production`: add required reviewers so every deployment needs an approval.
4. Actions > **Deploy to Azure** > Run workflow. The workflow provisions, deploys and smoke-tests `/health`.
5. To deploy automatically after every merge, add `push: branches: [main]` to the workflow's `on:` block - ideally
   gated on a green CI run.

## Operations

- **Logs:** Application Insights (Function invocations, web requests, exceptions) and the log stream of each app.
- **Data:** the Fetch log page; or Query editor on the database (sign in with an Entra account you add as admin, or
  temporarily switch the administrator).
- **Change cities:** set app settings `Weather__Cities__0__Name`, `Weather__Cities__0__CountryCode`, ... on the
  function app, or change `appsettings.json` and redeploy (`.claude/skills/manage-cities`).
- **Tear down everything (deletes all data and purges the Key Vault):**
  ```bash
  azd down --purge
  ```

## Known limits

- No data retention yet: the database grows by ~4,300 rows per day (Basic allows 2 GB).
- F1 web tier unloads after 20 idle minutes; the first request afterwards is slow.
- Migrations run on start (EF Core takes a lock); for larger teams prefer migration bundles in the pipeline.
