---
name: run-locally
description: Start the whole Weather Monitor stack on this machine (Azurite, Azure Functions host, ASP.NET Core Web + Vite) and prove it works end to end. Use when asked to run, start, demo or screenshot the app, or to check a change in the running app rather than only in tests.
---

# Run Weather Monitor locally

Goal: Functions fetches 3 cities every minute into LocalDB, and the UI at http://localhost:5173 shows them.

## 1. Preconditions (stop and tell the user if one fails)

```bash
dotnet --list-sdks                 # 10.0.x present
node -v                            # v22+ 
func --version                     # 4.x  (missing -> user installs Core Tools, see docs/getting-started.md)
sqllocaldb info                    # lists MSSQLLocalDB
```

API key - check presence only, NEVER print or echo its value:

```powershell
(dotnet user-secrets list --project src/WeatherMonitor.Functions) -match '^OpenWeatherMap:ApiKey = .+' | Measure-Object | % Count   # 1 = set
```

If it is not set, ask the user to run (they type the key, not you):
`dotnet user-secrets set "OpenWeatherMap:ApiKey" "<key>" --project src/WeatherMonitor.Functions`

## 2. Start the three processes (each long-running, in the background)

Prefer the desktop preview tool with `.claude/launch.json` (`azurite`, `functions`, `web`). Otherwise, from the repo root:

1. Azurite - skip if port 10000 already listens (Visual Studio may run it):
   `npx azurite --silent --location .azurite`
2. Functions: `dotnet run --project src/WeatherMonitor.Functions`
3. Web: `dotnet run --project src/WeatherMonitor.Web --launch-profile http` (the SPA proxy starts Vite on 5173)

Ports: Azurite 10000, Functions 7071, Web 5180 (http) / 7180 (https), Vite 5173. A port already in use means another
instance is running - find it before starting a second one.

## 3. Verify (all must hold)

- Functions output contains `Functions: FetchWeather: timerTrigger`, then `Weather ingestion completed: 3 succeeded, 0 failed`.
  Do not wait: `POST http://localhost:7071/admin/functions/FetchWeather` with JSON body `{"input":""}` -> 202.
- `GET http://localhost:5180/health/live` -> `Healthy`; `GET http://localhost:5180/health` -> `Healthy` after the
  first successful fetch (`Degraded` before it).
- Database (PowerShell, no sqlcmd needed):
  ```powershell
  $c = New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\MSSQLLocalDB;Database=WeatherMonitor;Integrated Security=True'
  $c.Open(); $q = $c.CreateCommand(); $q.CommandText = 'SELECT City, Country, MAX(FetchedAtUtc) FROM WeatherSnapshots GROUP BY City, Country'
  $t = New-Object Data.DataTable; $t.Load($q.ExecuteReader()); $c.Close(); $t
  ```
  Expect London GB, Riga LV, Rome IT. A wrong country means a missing or wrong `CountryCode` (ADR 0005).
- Browser: open http://localhost:5180/ (redirects to 5173), check **Temperature** and **Fetch log**, and the console has
  no errors besides StrictMode's aborted duplicate requests in development.

## 4. Stop

Stop the background processes you started (Web stops Vite with it). Leave processes the user started alone.

## Gotchas

- Data stops arriving while the Web host runs: only the Web project was started. In Visual Studio pick the
  **Web + Functions** profile; from the CLI start Azurite and the Functions host too.
- `SQL Server process failed to start`, or `MSSQLLocalDB` is shared with projects you must not touch: give this
  project its own instance (`sqllocaldb create WeatherMonitor -s`) and set `ConnectionStrings:WeatherDb` in user
  secrets of both hosts (`docs/getting-started.md`, Troubleshooting). Never restart or repair a shared instance.
- A new API key answers 401 for up to ~2 hours; failures then appear in the fetch log, which is correct behaviour.
- Typing a deep link on port 5180 returns 404 in development; navigate from `/` or use port 5173.
- The chart gets a new point only when the provider updates an observation (~every 10 minutes).
- Rebuilding while hosts run fails with locked DLLs (MSB3026/MSB3027) - stop the hosts first.
- When the Web host runs as a background process, the Vite instance the SPA proxy launches can hang (listens on 5173,
  answers nothing; `/health` on 5180 then hangs too, log: `Unable to connect to the SPA development server`). Reliable
  order for background runs: start Vite yourself first (`ASPNETCORE_URLS=http://localhost:5180`, then `npm run dev` in
  `src/weather-monitor.client`), wait for 5173 to answer, then start the Web host - the proxy reuses the running Vite.
