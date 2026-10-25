using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the host starts. Enabled with <c>Database:ApplyMigrationsOnStartup</c>.
/// It runs in <see cref="StartingAsync"/>, before any hosted service's <c>StartAsync</c> - including the Functions
/// worker that starts accepting timer invocations - so the first run never meets a database without tables.
/// Both hosts may start together (Web + Functions in development). EF Core serialises migrations with a lock inside the
/// database, but a missing database is created before that lock can exist: the host that loses that race gets SQL
/// error 1801 and migrates again, now against the existing database and under the lock.
/// When <c>Database:ReadOnlyIdentity</c> is configured (Azure), it also grants that identity read-only access.
/// </summary>
internal sealed partial class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrationService> logger) : IHostedLifecycleService
{
    /// <summary>SQL Server error "Database '...' already exists": another host created it a moment ago.</summary>
    private const int DatabaseAlreadyExists = 1801;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

        LogApplyingMigrations(logger);
        await MigrateAsync(dbContext, cancellationToken);

        if (options.Value.ReadOnlyIdentity is { IsConfigured: true } reader && dbContext.Database.IsSqlServer())
        {
            LogGrantingReadAccess(logger, reader.Name);
            await GrantReadOnlyAccessAsync(dbContext, reader, cancellationToken);
        }
    }

    private async Task MigrateAsync(WeatherDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == DatabaseAlreadyExists)
        {
            LogDatabaseCreatedConcurrently(logger);

            // Opening the then-missing database failed a moment ago, and SqlClient replays that failure to this pool
            // for a few seconds (pool blocking period); EF Core clears the pool only after a CREATE that succeeded.
            // Without this, the second attempt would see no database again and fail the same way.
            SqlConnection.ClearPool((SqlConnection)dbContext.Database.GetDbConnection());
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Creates a contained database user for an Entra identity from its client ID (TYPE = E with an explicit SID needs
    /// no directory permissions) and adds it to db_datareader. Idempotent; names are passed as parameters and quoted.
    /// </summary>
    private static Task<int> GrantReadOnlyAccessAsync(
        WeatherDbContext dbContext,
        ReadOnlyIdentityOptions reader,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
            DECLARE @name sysname = {reader.Name};
            DECLARE @sid varbinary(16) = CAST({reader.ClientId} AS varbinary(16));
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @name)
            BEGIN
                DECLARE @create nvarchar(max) =
                    N'CREATE USER ' + QUOTENAME(@name) + N' WITH SID = ' + CONVERT(nvarchar(64), @sid, 1) + N', TYPE = E;';
                EXEC sp_executesql @create;
            END;
            IF IS_ROLEMEMBER(N'db_datareader', @name) = 0
            BEGIN
                DECLARE @grant nvarchar(max) = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@name) + N';';
                EXEC sp_executesql @grant;
            END;
            """,
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying pending database migrations")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Another host created the database at the same time; applying the migrations again")]
    private static partial void LogDatabaseCreatedConcurrently(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granting read-only database access to {Identity}")]
    private static partial void LogGrantingReadAccess(ILogger logger, string identity);
}
