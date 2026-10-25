<#
.SYNOPSIS
    Creates a separate load-test database and fills it as if the function had fetched every minute for N days.

.DESCRIPTION
    Recreates the database (EF Core migrations, so the schema and indexes are the real ones), then inserts, day by
    day, one attempt per city per minute up to now: about 0.5% failures (HTTP 503 with a body, or a timeout without
    one), and for every success a snapshot with an OpenWeatherMap-shaped payload. The observation ("last update") only
    changes every 10 minutes per city, like the real provider, so repeated fetches return the same observation.

    The database name must contain "LoadTest": the script refuses to touch the development database.

.EXAMPLE
    ./tools/load-test/Seed-LoadTestData.ps1 -Days 31
    ./tools/load-test/Seed-LoadTestData.ps1 -Days 90   # the retention limit: the largest table the app ever keeps
#>
param(
    [ValidateRange(1, 400)]
    [int]$Days = 31,
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('LoadTest')]
    [string]$Database = 'WeatherMonitorLoadTest'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$connectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"

Add-Type -AssemblyName System.Data

# Dropped by its exact name through master: `dotnet ef database drop` has no --connection option and would drop the
# design-time database (the development one) instead.
Write-Host "Recreating $Database on $Server..."
$master = New-Object System.Data.SqlClient.SqlConnection("Server=$Server;Database=master;Trusted_Connection=True;TrustServerCertificate=True")
$master.Open()
try {
    $drop = $master.CreateCommand()
    $drop.CommandText = @'
IF DB_ID(@name) IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
                               + N'DROP DATABASE ' + QUOTENAME(@name) + N';';
    EXEC (@sql);
END
'@
    [void]$drop.Parameters.AddWithValue('@name', $Database)
    [void]$drop.ExecuteNonQuery()
}
finally {
    $master.Close()
}

# The schema and indexes come from the real migrations.
Push-Location $repositoryRoot
try {
    dotnet tool restore | Out-Null
    dotnet ef database update --project src/WeatherMonitor.Infrastructure --startup-project src/WeatherMonitor.Infrastructure `
        --connection $connectionString | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef database update failed.' }
}
finally {
    Pop-Location
}

# One day per batch keeps every transaction (and the log) small. #Rows holds one attempt per city per minute.
$insertDay = @'
SET NOCOUNT ON;

DECLARE @cities TABLE (City nvarchar(100), Country char(2), CityId int, Lat decimal(7,4), Lon decimal(7,4),
                       Base float, Amplitude float, ObservationOffset int);
INSERT INTO @cities VALUES
    (N'London', 'GB', 2643743, 51.5085, -0.1257, 11, 4, 0),
    (N'Riga',   'LV', 456172,  56.9460, 24.1059,  6, 5, 3),
    (N'Rome',   'IT', 3169070, 41.8947, 12.4839, 18, 6, 6);

WITH Minutes AS (
    SELECT TOP (1440) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Minute
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
Fetches AS (
    SELECT c.*, DATEADD(MINUTE, m.Minute, @dayStart) AS AttemptedAtUtc,
           DATEDIFF(MINUTE, @seriesStart, DATEADD(MINUTE, m.Minute, @dayStart)) + c.ObservationOffset AS Tick
    FROM Minutes m CROSS JOIN @cities c
    WHERE DATEADD(MINUTE, m.Minute, @dayStart) < @seriesEnd
),
Shaped AS (
    SELECT f.*,
           -- The provider refreshes each city every 10 minutes (at its own minute) and reports it ~2 minutes late.
           DATEADD(MINUTE, f.Tick - f.Tick % 10 - f.ObservationOffset - 2, @seriesStart) AS ObservedAtUtc,
           ABS(CHECKSUM(f.City, f.AttemptedAtUtc)) % 1000 AS Dice
    FROM Fetches f
)
SELECT s.City, s.Country, s.CityId, s.Lat, s.Lon, s.AttemptedAtUtc, s.ObservedAtUtc,
       CASE WHEN s.Dice < 5 THEN 0 ELSE 1 END AS IsSuccess,
       CASE WHEN s.Dice < 2 THEN NULL WHEN s.Dice < 5 THEN 503 ELSE 200 END AS HttpStatusCode,
       CASE WHEN s.Dice < 2 THEN 10000 + s.Dice ELSE 80 + ABS(CHECKSUM(s.AttemptedAtUtc)) % 400 END AS DurationMs,
       -- A daily cycle (coldest around 04:00 UTC) plus a little noise that only changes with the observation.
       ROUND(s.Base + s.Amplitude * SIN(2 * PI() * (DATEPART(HOUR, s.ObservedAtUtc) * 60
             + DATEPART(MINUTE, s.ObservedAtUtc) - 600) / 1440.0)
             + (ABS(CHECKSUM(s.City, s.ObservedAtUtc)) % 100 - 50) / 100.0, 2) AS Temperature
INTO #Rows
FROM Shaped s;

INSERT INTO dbo.FetchAttempts (City, AttemptedAtUtc, IsSuccess, HttpStatusCode, ErrorMessage, DurationMs, ResponseBody)
SELECT City, AttemptedAtUtc, IsSuccess, HttpStatusCode,
       CASE WHEN IsSuccess = 1 THEN NULL
            WHEN HttpStatusCode IS NULL THEN N'The request to the weather provider timed out after 10 seconds.'
            ELSE N'The weather provider answered 503 Service Unavailable.' END,
       DurationMs,
       CASE WHEN HttpStatusCode = 503 THEN N'<html><body><h1>503 Service Temporarily Unavailable</h1></body></html>' END
FROM #Rows;

INSERT INTO dbo.WeatherSnapshots
    (FetchAttemptId, City, Country, Temperature, TemperatureMin, TemperatureMax, ObservedAtUtc, FetchedAtUtc, RawPayload)
SELECT a.Id, r.City, r.Country, r.Temperature, r.Temperature - 1.2, r.Temperature + 0.9, r.ObservedAtUtc,
       r.AttemptedAtUtc,
       CONCAT(N'{"coord":{"lon":', r.Lon, N',"lat":', r.Lat,
              N'},"weather":[{"id":803,"main":"Clouds","description":"broken clouds","icon":"04d"}],"base":"stations",',
              N'"main":{"temp":', CAST(r.Temperature AS decimal(6,2)),
              N',"feels_like":', CAST(r.Temperature - 1.5 AS decimal(6,2)),
              N',"temp_min":', CAST(r.Temperature - 1.2 AS decimal(6,2)),
              N',"temp_max":', CAST(r.Temperature + 0.9 AS decimal(6,2)),
              N',"pressure":1012,"humidity":81,"sea_level":1012,"grnd_level":1008},"visibility":10000,',
              N'"wind":{"speed":4.63,"deg":250},"clouds":{"all":75},"dt":',
              DATEDIFF_BIG(SECOND, '1970-01-01', r.ObservedAtUtc),
              N',"sys":{"type":2,"id":2075535,"country":"', r.Country,
              N'","sunrise":1760941200,"sunset":1760978400},"timezone":3600,"id":', r.CityId,
              N',"name":"', r.City, N'","cod":200}')
FROM #Rows r
JOIN dbo.FetchAttempts a ON a.City = r.City AND a.AttemptedAtUtc = r.AttemptedAtUtc
WHERE r.IsSuccess = 1;

DROP TABLE #Rows;
'@

$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
try {
    # Whole minutes, so every run lines up with the minute-by-minute schedule of the real function.
    $now = [DateTime]::UtcNow
    $seriesEnd = New-Object DateTime($now.Year, $now.Month, $now.Day, $now.Hour, $now.Minute, 0, [DateTimeKind]::Utc)
    $seriesStart = $seriesEnd.AddDays(-$Days)
    $clock = [Diagnostics.Stopwatch]::StartNew()

    for ($day = 0; $day -lt $Days; $day++) {
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 0
        $command.CommandText = $insertDay
        $command.Parameters.Add('@seriesStart', [Data.SqlDbType]::DateTime2).Value = $seriesStart
        $command.Parameters.Add('@seriesEnd', [Data.SqlDbType]::DateTime2).Value = $seriesEnd
        $command.Parameters.Add('@dayStart', [Data.SqlDbType]::DateTime2).Value = $seriesStart.AddDays($day)
        [void]$command.ExecuteNonQuery()
        Write-Progress -Activity "Seeding $Database" -Status "Day $($day + 1) of $Days" -PercentComplete (100 * ($day + 1) / $Days)
    }
    Write-Progress -Activity "Seeding $Database" -Completed

    $command = $connection.CreateCommand()
    $command.CommandText = @'
SELECT (SELECT COUNT_BIG(*) FROM dbo.FetchAttempts), (SELECT COUNT_BIG(*) FROM dbo.FetchAttempts WHERE IsSuccess = 0),
       (SELECT COUNT_BIG(*) FROM dbo.WeatherSnapshots),
       (SELECT CAST(SUM(size) * 8 / 1024.0 AS decimal(10,1)) FROM sys.database_files WHERE type = 0)
'@
    $reader = $command.ExecuteReader()
    [void]$reader.Read()
    Write-Host ("Seeded {0} days in {1:n0} s: {2:n0} attempts ({3:n0} failed), {4:n0} snapshots, data file {5} MB." -f `
        $Days, $clock.Elapsed.TotalSeconds, $reader[0], $reader[1], $reader[2], $reader[3])
    $reader.Close()
}
finally {
    $connection.Close()
}
