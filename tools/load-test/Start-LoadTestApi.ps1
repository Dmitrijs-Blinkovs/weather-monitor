<#
.SYNOPSIS
    Runs a second Web API against the load-test database, next to (not instead of) the normal local run.

.DESCRIPTION
    Builds the Web project into a temporary folder (so a Visual Studio session that holds src/*/bin keeps running) and
    starts it on http://127.0.0.1:5280 against the load-test database, without the SPA proxy and without startup
    migrations (Seed-LoadTestData.ps1 created the schema). The per-client rate limit is raised so the measurement
    script measures the API, not the limiter. Stop it with Ctrl+C.

.EXAMPLE
    ./tools/load-test/Start-LoadTestApi.ps1
#>
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('LoadTest')]
    [string]$Database = 'WeatherMonitorLoadTest',
    [int]$Port = 5280
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$output = Join-Path ([IO.Path]::GetTempPath()) 'weather-monitor-load-test'

dotnet build (Join-Path $repositoryRoot 'src/WeatherMonitor.Web') --configuration Release --artifacts-path $output --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
$env:ASPNETCORE_HOSTINGSTARTUPASSEMBLIES = ''
$env:ConnectionStrings__WeatherDb = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
$env:Database__ApplyMigrationsOnStartup = 'false'
$env:RateLimiting__PermitsPerMinute = '1000000'

Write-Host "Load-test API on http://127.0.0.1:$Port against $Database (Ctrl+C to stop)."
& (Join-Path $output 'bin/WeatherMonitor.Web/release/WeatherMonitor.Web.exe') --contentRoot (Join-Path $repositoryRoot 'src/WeatherMonitor.Web')
