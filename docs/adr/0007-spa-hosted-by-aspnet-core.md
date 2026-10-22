# 0007. React SPA as an `.esproj`, served by the ASP.NET Core app

## Context

The assignment requires ASP.NET Core MVC and React/TypeScript, and the solution must open and run in Visual Studio 2026.

## Decision

Follow Visual Studio's "React and ASP.NET Core" layout: `weather-monitor.client` is a JavaScript project (`.esproj`,
Vite) referenced by `WeatherMonitor.Web`. The API is built with MVC controllers (`[ApiController]`). In development the
SPA proxy starts `npm run dev` and forwards the browser to Vite (port 5173), which proxies `/api` to the backend. On
publish the client is built and copied into `wwwroot`; `MapStaticAssets` serves it with precompressed files and
`MapFallbackToFile` handles client-side routes. Unknown `/api/*` routes return 404 problem details.

## Consequences

- One deployable web app; no CORS; one origin in production.
- Pages are lazy-loaded so the charting library only loads with the chart page.
- In development, typing a deep link on port 5180 returns 404 (only `/` redirects to Vite); use port 5173 or navigate.

## Alternatives considered

- Razor views hosting React islands with a Vite manifest tag helper: more moving parts for no functional gain.
- Separate static hosting (Azure Static Web Apps) + API: two deployments and CORS for a small app.
