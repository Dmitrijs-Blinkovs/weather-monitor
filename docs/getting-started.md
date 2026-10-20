# Getting started - set up the environment from scratch

Follow the steps in order; each ends with a check. Works for a human or for an AI assistant driving a terminal (see
[section 8](#8-setting-up-with-an-ai-assistant)). For running and debugging inside Visual Studio see
[debugging-in-visual-studio.md](debugging-in-visual-studio.md).

## 1. Prerequisites

| Tool | Version | Check | Install (Windows) |
|---|---|---|---|
| Windows | 10/11 | - | LocalDB is Windows-only; see the note below for macOS/Linux |
| .NET SDK | 10.0.100 or newer | `dotnet --list-sdks` | `winget install Microsoft.DotNet.SDK.10` (included with Visual Studio 2026) |
| Node.js | 22 LTS or newer | `node -v` | `winget install OpenJS.NodeJS.LTS` |
| SQL Server LocalDB | any current | `sqllocaldb info` lists `MSSQLLocalDB` | Visual Studio installer, or the SQL Server Express LocalDB installer |
| Azure Functions Core Tools | 4.x | `func --version` | `winget install Microsoft.Azure.FunctionsCoreTools` (asks for admin rights) or `npm i -g azure-functions-core-tools@4` |
| Azurite (storage emulator) | 3.x | - | bundled with Visual Studio; otherwise `npx azurite` (no install) |
| Git | any | `git --version` | `winget install Git.Git` |
| Visual Studio 2026 (optional) | 18.x | - | workloads **ASP.NET and web development** and **Azure development** |

After installing with winget, open a new terminal so the updated `PATH` is picked up.

> macOS/Linux: everything except LocalDB works. Run SQL Server in Docker
> (`docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=<pwd> -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest`)
> and override `ConnectionStrings:WeatherDb` for both hosts (user secrets for Web, `local.settings.json` value
> `ConnectionStrings__WeatherDb` for Functions).

## 2. Get the code

```bash
git clone https://github.com/Dmitrijs-Blinkovs/weather-monitor.git
```

```bash
cd weather-monitor
```

## 3. Get an OpenWeatherMap API key

1. Sign up at <https://home.openweathermap.org/users/sign_up> and confirm the e-mail.
2. Open <https://home.openweathermap.org/api_keys>; the **Default** key (32 characters) is already there.
3. A new key can take up to ~2 hours to activate. It is active when this URL (with your key) returns JSON instead of
   `401`: `https://api.openweathermap.org/data/2.5/weather?q=Riga,LV&units=metric&appid=<key>`

The free plan allows 60 calls/minute and 1,000,000 calls/month; this app uses 3 calls/minute (~130,000/month).

## 4. Store the key (never in a file under Git)

```bash
dotnet user-secrets set "OpenWeatherMap:ApiKey" "<your-key>" --project src/WeatherMonitor.Functions
```

Check (prints the key - do not paste the output anywhere):

```bash
dotnet user-secrets list --project src/WeatherMonitor.Functions
```

Visual Studio alternative: right-click `WeatherMonitor.Functions` > **Manage User Secrets** and save
`{ "OpenWeatherMap:ApiKey": "<your-key>" }`.

## 5. Restore, build and test

```bash
dotnet tool restore
```

```bash
dotnet build WeatherMonitor.slnx
```

```bash
dotnet test --solution WeatherMonitor.slnx
```

```bash
cd src/weather-monitor.client && npm ci && npm test
```

Browser tests (downloads Chromium once, then builds and previews the app itself):

```bash
cd src/weather-monitor.client && npm run test:e2e:install && npm run test:e2e
```

Expected: build with 0 warnings; all tests green. None of these need the key, the database or Azure.

## 6. Run

Three processes. From the repository root, one terminal each:

```bash
npx azurite --silent --location .azurite
```

```bash
dotnet run --project src/WeatherMonitor.Functions
```

```bash
dotnet run --project src/WeatherMonitor.Web --launch-profile https
```

- The first start of either host creates the `WeatherMonitor` database in LocalDB (migrations run automatically in
  development).
- The Web host starts Vite (`npm run dev`) itself and the browser at <https://localhost:7180> is forwarded to
  <http://localhost:5173>. If the browser warns about the certificate, run `dotnet dev-certs https --trust` once.
- In Visual Studio: pick the **Web + Functions** profile and press F5 (Azurite starts automatically).

## 7. Verify

| Check | Expected |
|---|---|
| Functions terminal | `FetchWeather: timerTrigger` and `PurgeOldData: timerTrigger`, then every minute `Weather ingestion completed: 3 succeeded, 0 failed` |
| <http://localhost:5180/health/live> | `Healthy` (the web process runs) |
| <http://localhost:5180/health> | `Healthy` once a fetch has succeeded; `Degraded` before the first fetch or when none succeeded for 10 minutes |
| UI > **Fetch log** | three rows per minute, `Success`, HTTP 200 |
| UI > **Temperature** | a card per city (London GB, Riga LV, Rome IT) and lines on the chart |

Do not want to wait for the next minute? Trigger the function directly:

```bash
curl -X POST http://localhost:7071/admin/functions/FetchWeather -H "Content-Type: application/json" -d "{\"input\":\"\"}"
```

The chart gets a new point roughly every 10 minutes (that is how often the provider updates an observation), so give it
an hour for a meaningful line. Query the database directly:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -d WeatherMonitor -Q "SELECT City, IsSuccess, COUNT(*) FROM FetchAttempts GROUP BY City, IsSuccess"
```

## 8. Setting up with an AI assistant

Any coding agent (Claude Code, Codex, Cursor, Copilot...) can do steps 1-7. Give it this prompt:

> Read `AGENTS.md` and `docs/getting-started.md` in this repository. Check the prerequisites and tell me what is
> missing before installing anything. Do not ask for, read or print my OpenWeatherMap API key - I will set it myself
> with `dotnet user-secrets`; you may only check whether `OpenWeatherMap:ApiKey` is present. Then build, run all tests,
> start Azurite, the Functions host and the Web host, and verify with the checks in section 7.

Claude Code users get the same procedure as a skill: `.claude/skills/run-locally`. Installing Core Tools with winget
shows a Windows administrator prompt that only you can approve.

## 9. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Functions host stops with `OpenWeatherMap:ApiKey is not configured` | Step 4 not done, or done for another project. Re-run the command with `--project src/WeatherMonitor.Functions`. |
| Fetch log shows `Failed`, HTTP 401 | Key not active yet (wait up to ~2 h) or mistyped. |
| HTTP 404 for one city | Misspelt `Name` or wrong `CountryCode` in `src/WeatherMonitor.Functions/appsettings.json`. |
| HTTP 429 | Rate limit; another app is using the same key. |
| Functions host: `Microsoft.Azure.Storage ... connection refused` / timer does not fire | Azurite is not running. |
| `A network-related or instance-specific error ... LocalDB` | `sqllocaldb start MSSQLLocalDB`; check `sqllocaldb info MSSQLLocalDB`. |
| Requests to 5180 hang, Web log says `Unable to connect to the SPA development server` | The Vite process started by the SPA proxy hung. Stop the Web host, start Vite yourself in `src/weather-monitor.client` with `npm run dev` (for the `http` profile set `ASPNETCORE_URLS=http://localhost:5180` first so Vite proxies the API there), then start the Web host again - the proxy reuses the running Vite. |
| Port 5173/5180/7071/7180 already in use | Another instance is running; stop it (Visual Studio Shift+F5 or close the terminal). |
| `func` not found after winget install | Open a new terminal (PATH refresh). |
| `Testing with VSTest target is no longer supported` | Use `dotnet test --solution ...` / `--project ...` (Microsoft.Testing.Platform, configured in `global.json`). |
| A city shows the wrong country | Its `CountryCode` is missing or wrong in `Weather:Cities` (a bare `Rome` is Rome, Georgia, USA). Fix it (`.claude/skills/manage-cities`); rows stored earlier keep the old country - reset the local database (below) if that matters. |
| Build error `MSB3030: Could not copy ... WorkerExtensions ... because it was not found` | The clone path is too deep for Windows' 260-character limit (the Functions build nests long paths). Clone to a short path such as `C:\src\weather-monitor`, or enable long paths in Windows. |

Reset the local database (it is recreated on the next start):

```bash
dotnet ef database drop --force --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure
```
