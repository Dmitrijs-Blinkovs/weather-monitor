# Deploying to Azure

Status: deployment-ready, not deployed. The infrastructure templates (`infra/`), the azd definition (`azure.yaml`) and
the deployment workflow (`.github/workflows/deploy.yml`) are complete and checked in CI: Bicep compiles with strict
linter rules (`infra/bicepconfig.json`) and the migrations run on SQL Server 2022. The first deployment is a
deliberate, manual step. The topology diagram is
in the [README](../README.md#azure).

## What gets created

One resource group `rg-<environment>` with:

| Resource | SKU | Purpose | Approx. cost / month |
|---|---|---|---|
| Managed identity `id-func-*` | - | the function: SQL administrator (migrations), Key Vault secret reader, Storage | free |
| Managed identity `id-web-*` | - | the web app: `db_datareader` on the database, nothing else | free |
| Function app (Linux) | Flex Consumption FC1, 512 MB | `FetchWeather` every minute, `PurgeOldData` daily at 03:30 UTC | ~0 (inside the monthly free grant) |
| Storage account | Standard LRS, shared keys disabled | timer leases, Flex deployment packages | cents |
| App Service plan + web app (Linux) | B1 (default) or F1 | MVC API + React UI, health probe `/health/live` | ~13 USD (B1) / 0 (F1) |
| Azure SQL server + database `weatherdb` | Basic (5 DTU, 2 GB) | fetch log and snapshots | ~5 USD |
| Key Vault | Standard, RBAC | the OpenWeatherMap API key | cents |
| Log Analytics + Application Insights | pay-as-you-go | traces, metrics, logs, Key Vault and SQL audit logs | free up to 5 GB/month |
| Scheduled query alert (+ action group if `ALERT_EMAIL` is set) | - | fires when no fetch succeeded for 15 minutes | ~0.10 USD |

Why Basic SQL and not serverless: a database written every minute never auto-pauses, so the serverless free offer
(100,000 vCore-seconds) would be used up within days.

Volume: three cities every minute are ~4,300 attempts plus ~4,300 snapshots per day (~8,600 rows). The daily purge
keeps 90 days ([ADR 0014](adr/0014-data-retention.md)), i.e. well under 1 GB of the 2 GB Basic limit.

## Security model

Details and the OWASP mapping: [security.md](security.md).

- **No passwords anywhere.** Each app has its own user-assigned managed identity
  ([ADR 0015](adr/0015-least-privilege-identities.md)). SQL accepts only Entra ID; Storage shared keys are disabled.
- **Least privilege.** The function identity is the SQL Entra administrator (it applies the migrations on start) and,
  after migrating, grants the web identity `db_datareader` (`Database__ReadOnlyIdentity__*` app settings). The web app
  can read, not write; it cannot read the Key Vault or the storage account.
- **The API key** is a Key Vault secret; the function's app setting is a Key Vault reference.
- **Network.** HTTPS only (TLS 1.3 on the web app, 1.2 elsewhere), FTP and basic-auth publishing disabled, the function
  app's site and admin API deny all inbound traffic (it only has timers; deployment goes through the Entra-protected
  SCM endpoint), SQL is reachable from Azure services only.
- **Audit.** Key Vault and SQL audit logs go to Log Analytics.

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

Optional: e-mail for the "no weather data" alert.

```bash
azd env set ALERT_EMAIL <address>
```

Optional, free web tier (slower first request, no health probe):

```bash
azd env set WEB_SKU_NAME F1
```

```bash
azd up
```

`azd up` provisions `infra/main.bicep` and deploys both services (`azure.yaml`). It prints `WEB_URL` at the end.
The azd environment (including the key) is stored under `.azure/`, which is ignored by Git.

Verify:
1. `<WEB_URL>/health/live` returns `Healthy` (the process runs).
2. `<WEB_URL>/health` returns `Healthy` within a few minutes: the database is reachable and a fetch succeeded in the
   last 10 minutes. `Degraded` means no recent successful fetch - see `.claude/skills/troubleshoot-ingestion`.
3. The Fetch log shows three successful attempts per minute.
4. Application Insights > Metrics (custom namespace) shows `weather.fetch.attempts` split by `outcome`.

## Continuous deployment from GitHub

The workflow runs automatically after every green CI run on `main` (and can be started manually from `main`). It is
skipped until the repository variable `AZURE_ENV_NAME` exists, and fails with a clear message if the others are missing.

1. Connect the repository to Azure once. `azd pipeline config` creates an app registration (or managed identity) with
   federated credentials, assigns it roles and sets the repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
   `AZURE_SUBSCRIPTION_ID`, `AZURE_ENV_NAME`, `AZURE_LOCATION`:
   ```bash
   azd pipeline config
   ```
2. **Tighten the federated credentials.** The deploy job runs in the `production` environment, so its OIDC subject is
   `repo:<owner>/<repo>:environment:production`. Keep exactly one federated credential with that subject on the app
   registration (Entra ID > App registrations > Certificates & secrets > Federated credentials) and delete the
   branch and `pull_request` credentials azd creates: a pull request must never be able to obtain an Azure token.
   Where possible, scope the principal's role assignments to the resource group instead of the subscription.
3. Settings > Environments > `production`:
   - add **required reviewers** and allow deployments from the `main` branch only;
   - add the **environment secret** `OPENWEATHERMAP_API_KEY` (not a repository secret: only the approved deploy job
     can read it). If `azd pipeline config` created a repository secret of the same name, delete it.
4. Optional repository variables: `WEB_SKU_NAME` (`B1` default, or `F1`), `ALERT_EMAIL`.
5. Merge to `main`. The workflow builds the packages in a job without any credential, waits for the approval, then
   provisions, deploys the packages built from that commit and smoke-tests `/health` (must report `Healthy` within 5 minutes).

## Operations

- **Telemetry:** Application Insights - function invocations with their OpenWeatherMap calls, web requests with their
  SQL calls, the
  `weather.fetch.attempts` / `weather.fetch.duration` metrics per city and outcome, exceptions, logs.
- **Alert:** `alert-no-weather-data-*` fires when no successful fetch was recorded for 15 minutes (revoked key, quota,
  provider outage, broken deployment). Without `ALERT_EMAIL` it is visible in the portal only.
- **Health:** `/health/live` (App Service probe; process only) and `/health` (database + data freshness; for humans,
  monitors and the smoke test).
- **Data:** the Fetch log page; or Query editor on the database (sign in with an Entra account you add as admin, or
  temporarily switch the administrator).
- **Retention:** `Retention__Days` on the function app (default 90, `0` disables the purge).
- **Trigger a function manually:** the function app denies inbound traffic, so the portal's "Test/Run" and the admin
  API are closed. Add a temporary access restriction for your IP if you really need it, and remove it afterwards.
- **Change cities:** change `src/WeatherMonitor.Functions/appsettings.json` and redeploy
  (`.claude/skills/manage-cities`). App settings (`Weather__Cities__0__Name`, ...) override entries by index and can
  add cities, but cannot remove one from the shipped list.
- **Rotate the API key:** set the new value as the environment secret (or `azd env set OPENWEATHERMAP_API_KEY`) and run
  the deployment; the versionless Key Vault reference picks up the new version within 24 hours, or at once after a
  restart of the function app.
- **Tear down everything (deletes all data and purges the Key Vault):**
  ```bash
  azd down --purge
  ```

## Hardening options

Accepted for this size, recommended for a long-lived production environment (see [security.md](security.md)):

| Option | Effect | Cost |
|---|---|---|
| Private endpoints for SQL, Key Vault and Storage + VNet integration of both apps, `publicNetworkAccess: Disabled` | removes the "any Azure network" SQL firewall rule | ~10-20 USD/month |
| Key Vault purge protection | a deleted vault/secret cannot be purged during the soft-delete retention (7 days as configured; blocks `azd down --purge` + re-create) | free |
| An Entra group as SQL administrator, migration bundles in the pipeline | the function no longer needs admin rights at run time | free |
| `DisableLocalAuth` on Application Insights + Monitoring Metrics Publisher role | telemetry accepted only with Entra tokens | free |
| Microsoft Defender for SQL / App Service | threat detection, vulnerability assessment | paid |

## Extending in Azure

The templates are one Bicep file per scope and every app setting is plain configuration, so most extensions are a few
lines in `infra/resources.bicep` or an app setting:

| Need | How | Where |
|---|---|---|
| A second environment (staging, previews) | `azd env new weather-staging` creates its own resource group; for GitHub add a `staging` environment with its own federated credential (`...:environment:staging`) and a deploy job that targets it | `azure.yaml`, `deploy.yml` |
| Zero-downtime web releases | A `staging` deployment slot on the web app (Standard plan or higher), smoke-tested and then swapped | web app in `resources.bicep` |
| More cities | App settings `Weather__Cities__3__Name` / `__CountryCode` add a city (arrays merge by index), or change `appsettings.json` and redeploy | `.claude/skills/manage-cities` |
| A new secret | A Key Vault secret, an `@Microsoft.KeyVault(SecretUri=...)` app setting, and `Key Vault Secrets User` for the one identity that reads it | `resources.bicep` |
| Private networking | A VNet with integration subnets for both apps, private endpoints and private DNS zones for SQL, Key Vault and Storage, `publicNetworkAccess: 'Disabled'` | [Hardening options](#hardening-options) |
| Sign-in for the UI and API | App Service built-in authentication with Entra ID (`authsettingsV2`), or `Microsoft.Identity.Web` in code | web app in `resources.bicep` |
| More alerts | Scheduled query or metric alerts next to `noWeatherDataAlert`: failed invocations, HTTP 5xx rate, SQL DTU | `resources.bicep` |
| Scale | Flex `maximumInstanceCount` / `instanceMemoryMB`, the App Service plan SKU and instance count, the SQL tier | `resources.bicep` (the web plan SKU also via `WEB_SKU_NAME` in `main.parameters.json`) |
| Another weather provider | A new `IWeatherProvider` adapter with its options in Infrastructure; Bicep changes only for its secret | `AGENTS.md`, ADR 0001 |
| Migrations from the pipeline | `dotnet ef migrations bundle` in the build job, run by the deploy job with an Entra admin group; the function then needs no admin rights | ADR 0015 |

## Known limits

- F1 web tier unloads after 20 idle minutes; the first request afterwards is slow, and there is no health probe.
- Migrations run on start (EF Core takes a lock); for larger teams prefer migration bundles in the pipeline.
- One environment (`production`); a second azd environment or a slot would allow testing before production
  ([Extending in Azure](#extending-in-azure)).
