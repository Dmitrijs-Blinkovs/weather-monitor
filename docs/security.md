# Security

How Weather Monitor addresses the OWASP Top 10 (2021/2025), the OWASP API Security Top 10 (2023) and the OWASP Top 10
CI/CD Security Risks, which risks are accepted, and what is deliberately out of scope. Keep this page current when a
control changes. Last full review: 2026-10-20 (independent static review of code, infrastructure and pipelines).

## Threat model in short

| Asset | Threat | Main controls |
|---|---|---|
| OpenWeatherMap API key | leakage through source control, logs, error messages, telemetry, pipelines | user secrets / Key Vault reference, no default HttpClient URI logging, redacted provider errors, exception texts never stored, query strings redacted in traces, key visible only to the provision step |
| Availability of the API and the database | request floods, deep pages, huge ranges, unbounded third-party responses, unbounded growth | per-client rate limit + global concurrency limit, output cache, page/range caps, index for result filters, 64 KB response cap, function timeout, daily retention purge |
| Integrity of stored data | wrong city resolved, malformed or implausible provider data | explicit country codes, content-type check, strict parsing, plausibility checks (invalid data becomes a failed attempt) |
| Browser users | XSS, clickjacking, MIME sniffing, referrer leakage | React escaping, no HTML sinks, strict CSP (verified in a real browser), `frame-ancestors 'none'`, `nosniff`, referrer policy, headers also on error responses |
| Cloud resources | credential theft, lateral movement, unaudited access | one managed identity per app with least privilege, Entra-only SQL, shared keys and basic-auth publishing disabled, function closed to inbound traffic, Key Vault and SQL audit logs |
| Build and release | tampered dependencies or actions, stolen deployment credentials | lockfiles, CPM, NuGet/npm audit gates, Dependabot with cooldown, actions pinned to SHAs, OIDC, credential-free build job, protected `production` environment |

Out of scope by design: user authentication and authorisation. The application shows public weather data read-only;
there are no write endpoints and no personal data. If that changes, add Entra ID authentication (App Service
authentication or `Microsoft.Identity.Web`) before exposing anything non-public.

## OWASP Top 10

| Category | Status | How |
|---|---|---|
| A01 Broken access control | OK | No write endpoints, no user data, same-origin only (no CORS). Least privilege in Azure: the public web app only has `db_datareader` (ADR 0015); the function is closed to inbound traffic. |
| A02 Cryptographic failures | OK | HTTPS only with HSTS (1 year), TLS 1.3 on the web app, TLS 1.2 elsewhere; `BaseUrl` must be https; SQL encrypted at rest (TDE) and in transit; the only secret is in Key Vault. |
| A03 Injection | OK | EF Core LINQ only; the single raw statement (read-only grant) is parameterised and uses `QUOTENAME`; React escapes all output; strict CSP; control characters stripped from stored error texts (log forging). |
| A04 Insecure design | OK | Failure isolation per city, validated configuration, caps on every input, retention purge (ADR 0014), cancellation-safe logging. |
| A05 Security misconfiguration | OK | Security headers on every response including 500s; OpenAPI only in development; RFC 7807 errors without details; `AllowedHosts` restricted in Azure; FTP/basic-auth publishing disabled. |
| A06 Vulnerable and outdated components | OK | NuGet Audit (direct + transitive) fails the build on high/critical; `npm audit --omit=dev --audit-level=high` in CI; Dependabot weekly with a 3-day cooldown; currently 0 known vulnerabilities. |
| A07 Identification and authentication failures | n/a / OK | No user accounts; services authenticate with managed identities (no stored credentials). |
| A08 Software and data integrity failures | OK | Lockfile + `npm ci`, CPM, actions pinned to commit SHAs, checkout without persisted credentials, OIDC deployments, build job without credentials, environment approval. |
| A09 Logging and monitoring failures | OK | Every fetch attempt stored; OpenTelemetry traces/metrics/logs to Application Insights; Key Vault and SQL audit logs; data-freshness health check; alert on 15 minutes without data (ADR 0016). |
| A10 Server-side request forgery | OK | The only outbound call goes to the configured https base URL; user input never reaches it; city names are URL-encoded. |

## OWASP API Security Top 10 (2023)

| Risk | Status | How |
|---|---|---|
| API1/API5 object- and function-level authorisation | n/a | Public, read-only, no per-user objects. |
| API2 Broken authentication | n/a | No authentication by design. |
| API3 Broken object property level authorisation | OK | DTOs expose display fields only; raw payloads are never returned; request models are query-only (no mass assignment). |
| API4 Unrestricted resource consumption | OK | 120 requests/minute per client on `/api/*` and `/health` (429 + `Retry-After`), global concurrency limit (64 + queue 128), output cache (history 60 s, log 10 s, cities 5 min), `pageSize` <= 200, `page` <= 1,000, history range <= 31 days, index for result filters, response compression. |
| API6 Unrestricted access to sensitive business flows | n/a | No business flows. |
| API7 Server-side request forgery | OK | See A10. |
| API8 Security misconfiguration | OK | See A05; unknown `/api/*` routes return 404, wrong verbs 405. |
| API9 Improper inventory management | OK | One API version, documented in the README; OpenAPI only in development. |
| API10 Unsafe consumption of APIs | OK | 64 KB response cap, `application/json` required, required fields and plausible temperatures validated, error texts redacted and sanitised, bounded retries/timeouts and a tuned circuit breaker. |

## OWASP Top 10 CI/CD Security Risks

| Risk | How |
|---|---|
| CICD-SEC-1/2 Flow control, identity and access | Deploys only from `main` after green CI; `production` environment with required reviewers; one federated credential with the environment subject (docs/deployment.md). |
| CICD-SEC-3 Dependency chain abuse | Lockfiles, CPM, audit gates, Dependabot cooldown; the job that runs third-party build code has no credentials. |
| CICD-SEC-4 Poisoned pipeline execution | No `pull_request_target`; PR workflows get a read-only token and no secrets; the `workflow_run` deployment accepts only CI runs of pushes to this repository (its branch filter alone would also match a fork's branch named `main`). |
| CICD-SEC-6 Insufficient credential hygiene | No stored Azure secrets (OIDC); the API key is an environment secret exposed to one step. |
| CICD-SEC-8 Ungoverned third-party services | Actions pinned to commit SHAs; only well-known publishers. |

## Accepted risks and further hardening

| Item | Why accepted now | Option |
|---|---|---|
| SQL reachable from any Azure network (`AllowAllWindowsAzureIps`) | Entra-only authentication still required; private networking adds ~10-20 USD/month | Private endpoint + VNet integration for both apps, `publicNetworkAccess: Disabled` |
| Function identity is the SQL administrator | Needed for migrations on start; the function has no inbound access | Entra group as admin and migration bundles in the pipeline (ADR 0015) |
| Key Vault without purge protection (7-day soft delete) | The only secret is a free, easily rotated API key; purge protection blocks `azd down --purge` + re-create for 90 days | `enablePurgeProtection: true` for long-lived environments |
| Application Insights accepts its connection string (local auth) | Telemetry spoofing has low impact here | `DisableLocalAuth: true` + Monitoring Metrics Publisher role + Entra auth in the exporters |
| No Microsoft Defender for SQL / for App Service | Paid plans | Enable at subscription level for production subscriptions |
| No GitHub dependency review / CodeQL | Require GitHub Advanced Security on private repositories | Enable if the repository becomes public or GHAS is available |

## Implementation pointers

- Headers: `src/WeatherMonitor.Web/Security/SecurityHeaders.cs` (mirrored for the browser tests in
  `src/weather-monitor.client/vite.config.ts`); tests: `SecurityTests`, Playwright "strict content security policy".
- Rate limiting: `src/WeatherMonitor.Web/Security/ApiRateLimiting.cs` (`RateLimiting:PermitsPerMinute`); caching and
  compression: `src/WeatherMonitor.Web/Caching/ApiCaching.cs`.
- Key handling: `OpenWeatherMapClient` (`Redact`), `AddOpenWeatherMap` (`RemoveAllLoggers`, response cap),
  `WeatherIngestionService` (exception texts logged, not stored), `FetchAttempt.SanitizeErrorMessage`.
- Azure: `infra/resources.bicep` (identities, Key Vault reference, Entra-only SQL, auditing, network restrictions).
- Pipelines: `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`, `.github/dependabot.yml`.

## Reporting a vulnerability

Do not open a public issue. Contact the repository owner privately (GitHub profile of the owner of this repository).
