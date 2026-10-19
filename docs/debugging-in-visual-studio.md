# Running and debugging in Visual Studio 2026

Step-by-step manual procedure. Prerequisites and the API key are covered in
[getting-started.md](getting-started.md) (sections 1, 3 and 4).

## 1. One-time setup

1. **Visual Studio Installer** > Modify > make sure these workloads are installed:
   **ASP.NET and web development** (includes LocalDB) and **Azure development** (Functions tools and Azurite).
   Node.js 22+ must be installed separately.
2. Open `WeatherMonitor.slnx` (File > Open > Project/Solution).
3. Set the API key: in **Solution Explorer** right-click `src/WeatherMonitor.Functions` > **Manage User Secrets** and
   save:
   ```json
   { "OpenWeatherMap:ApiKey": "<your-key>" }
   ```
4. Build once (**Ctrl+Shift+B**). The first build restores NuGet packages and runs `npm install` for the client
   project; it takes a minute.
5. If Visual Studio asks to trust the ASP.NET Core development certificate, answer **Yes**.

## 2. Choose what to start

The repository ships a shared startup profile in `WeatherMonitor.slnLaunch`:

1. In the toolbar, open the startup dropdown next to the green **Start** button.
2. Select **Web + Functions**.

If the profile is not listed (older settings), create it once:

1. Right-click the solution > **Configure Startup Projects...**
2. Choose **Multiple startup projects**.
3. Set `WeatherMonitor.Functions` > **Start** and `WeatherMonitor.Web` > **Start** (debug target `https`). Leave
   `weather-monitor.client` on **None** - the web app starts Vite itself.
4. Optionally save it as a launch profile named "Web + Functions".

To debug only one host, right-click the project > **Set as Startup Project**:
- only `WeatherMonitor.Functions` - fetching and storing; the UI is not needed.
- only `WeatherMonitor.Web` - API and UI against the data already in the database.

## 3. Start (F5)

What happens:

1. Visual Studio starts **Azurite** (needed by the timer) automatically.
2. A console window opens for the **Functions host** (port 7071). Wait for `Functions: FetchWeather: timerTrigger`.
   At second 0 of every minute it logs `Weather ingestion completed: 3 succeeded, 0 failed`.
3. A console window opens for the **Web host** (ports 7180/5180). It applies EF Core migrations (creates the
   `WeatherMonitor` database in LocalDB on the first run) and starts **Vite** (port 5173).
4. The browser opens <https://localhost:7180>; the page "Launching the SPA proxy..." forwards to
   <http://localhost:5173>. Use the **Temperature** and **Fetch log** links.

Stop everything with **Shift+F5**.

## 4. Useful breakpoints

| Where | What you see |
|---|---|
| `src/WeatherMonitor.Functions/FetchWeatherFunction.cs` - `RunAsync` | every timer tick (`timer.IsPastDue` when late) |
| `src/WeatherMonitor.Application/Ingestion/WeatherIngestionService.cs` - `FetchAsync` | one call per city, its result and duration |
| `src/WeatherMonitor.Infrastructure/OpenWeatherMap/OpenWeatherMapClient.cs` - after `ReadAsStringAsync` | raw HTTP status and body from OpenWeatherMap |
| `src/WeatherMonitor.Infrastructure/OpenWeatherMap/OpenWeatherMapClient.cs` - `TryParse` | extraction of country, temperatures and `dt` |
| `src/WeatherMonitor.Infrastructure/Persistence/FetchAttemptRepository.cs` - `AddRangeAsync` | the attempts and snapshots being saved |
| `src/WeatherMonitor.Web/Controllers/WeatherController.cs` - `GetHistoryAsync` | bound and validated `from`/`to` |
| `src/WeatherMonitor.Application/History/WeatherHistoryService.cs` - `BuildSeries` | grouping and summary per city |
| `src/WeatherMonitor.Infrastructure/Persistence/WeatherHistoryReader.cs` | the `DISTINCT` query result |

Tip: to inspect the SQL that EF Core sends, set `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` in
`src/WeatherMonitor.Web/appsettings.json` or `src/WeatherMonitor.Functions/appsettings.json`.

## 5. Trigger the function without waiting

Open `src/WeatherMonitor.Functions/WeatherMonitor.Functions.http` and click **Send request** above
`POST .../admin/functions/FetchWeather` (Visual Studio's built-in HTTP client). Breakpoints in the ingestion path are
hit immediately. `src/WeatherMonitor.Web/WeatherMonitor.Web.http` has ready-made requests for every API endpoint,
including invalid ones that return `400`.

## 6. Look at the data

**View > SQL Server Object Explorer** > `(localdb)\MSSQLLocalDB` > Databases > `WeatherMonitor` > Tables.
Right-click a table > **View Data**, or **New Query** and run:

```sql
SELECT TOP 20 * FROM FetchAttempts ORDER BY Id DESC;

SELECT City, Country, Temperature, TemperatureMin, TemperatureMax, ObservedAtUtc, FetchedAtUtc,
       JSON_VALUE(RawPayload, '$.name') AS ProviderName
FROM WeatherSnapshots ORDER BY Id DESC;
```

## 7. Debug the front end

- Use the browser's DevTools on <http://localhost:5173>: **Sources** shows the original `.tsx` files (Vite source
  maps), **Network** shows the `/api/*` calls. Edits to the client hot-reload without restarting anything.
- React state: install the React Developer Tools browser extension.
- Visual Studio can also debug the client: set breakpoints in `.tsx` files when the browser was started by Visual Studio
  (Edge/Chrome with script debugging enabled under Tools > Options > Debugging > General).

## 8. Run tests

- **Test > Test Explorer** > Run All. .NET tests use xUnit v3 on Microsoft.Testing.Platform (supported natively by
  Visual Studio 2026). Front-end Vitest tests appear under `weather-monitor.client` when the JavaScript test adapter is
  enabled; otherwise run them in **View > Terminal**:
  ```bash
  cd src/weather-monitor.client && npm test
  ```
- Debug a single test: right-click it in Test Explorer > **Debug**.

## 9. Troubleshooting in Visual Studio

| Symptom | Fix |
|---|---|
| Functions console closes immediately with `OpenWeatherMap:ApiKey is not configured` | Step 1.3 (Manage User Secrets on the Functions project). |
| "Web + Functions" not in the dropdown | Create it as in section 2. |
| Browser stays on "Launching the SPA proxy..." | First `npm install` still running or Node.js missing; check the Web console window. If the log says `Unable to connect to the SPA development server`, stop debugging, run `npm run dev` in `src/weather-monitor.client` from **View > Terminal**, then press F5 again - the proxy reuses the running Vite. |
| Port already in use | A previous run is still alive: Shift+F5, or close stray `func`, `dotnet` and `node` processes. |
| Timer never fires, storage connection errors in the Functions console | Azurite is not running. Restart debugging, or start it manually from the repository root: `npx azurite --silent --location .azurite`. |
| Warning `NU190x` | A package with a known vulnerability was reported; pin a fixed version in `Directory.Packages.props`. |
