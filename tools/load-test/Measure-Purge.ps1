<#
.SYNOPSIS
    Times the retention purge on the load-test database: everything older than -RetentionDays, in batches.

.DESCRIPTION
    Runs the statement FetchAttemptRepository.DeleteOlderThanAsync sends (EF Core ExecuteDelete: the oldest -BatchSize
    attempts before the cutoff, their snapshots by ON DELETE CASCADE) until a batch comes back short, exactly like
    DataRetentionService. Reports the batches, rows and time, to compare with the Functions timeout (host.json, 55 s).
    Deletes data: the database name must contain "LoadTest".

.EXAMPLE
    ./tools/load-test/Measure-Purge.ps1 -RetentionDays 30   # a 90-day database after retention was cut to 30 days
#>
param(
    [ValidateRange(1, 400)]
    [int]$RetentionDays = 30,
    [ValidateRange(1, 100000)]
    [int]$BatchSize = 5000,
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('LoadTest')]
    [string]$Database = 'WeatherMonitorLoadTest'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

# The shape EF Core generates for Where(AttemptedAtUtc < cutoff).OrderBy(AttemptedAtUtc).Take(n).ExecuteDeleteAsync().
$deleteBatch = @'
DELETE FROM [f]
FROM [FetchAttempts] AS [f]
WHERE [f].[Id] IN (
    SELECT TOP(@take) [f0].[Id]
    FROM [FetchAttempts] AS [f0]
    WHERE [f0].[AttemptedAtUtc] < @cutoff
    ORDER BY [f0].[AttemptedAtUtc]
)
'@

$connection = New-Object System.Data.SqlClient.SqlConnection(
    "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True")
$connection.Open()
try {
    $cutoff = [DateTime]::UtcNow.AddDays(-$RetentionDays)
    $batches = 0
    $total = 0
    $slowest = 0.0
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 0
        $command.CommandText = $deleteBatch
        $command.Parameters.Add('@take', [Data.SqlDbType]::Int).Value = $BatchSize
        $command.Parameters.Add('@cutoff', [Data.SqlDbType]::DateTime2).Value = $cutoff
        $batchClock = [Diagnostics.Stopwatch]::StartNew()
        $deleted = $command.ExecuteNonQuery()
        $slowest = [Math]::Max($slowest, $batchClock.Elapsed.TotalMilliseconds)
        if ($deleted -gt 0) { $batches++ }
        $total += $deleted
    } while ($deleted -eq $BatchSize)
    $clock.Stop()

    "Purged {0:n0} attempts older than {1} days (with their snapshots) in {2} batches of up to {3:n0}: {4:n1} s in total, slowest batch {5:n0} ms." -f `
        $total, $RetentionDays, $batches, $BatchSize, $clock.Elapsed.TotalSeconds, $slowest
}
finally {
    $connection.Close()
}
