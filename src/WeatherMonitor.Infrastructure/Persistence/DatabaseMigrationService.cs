using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the host starts. Enabled with <c>Database:ApplyMigrationsOnStartup</c>.
/// EF Core takes a database lock while migrating, so the web app and the function app can both run it safely.
/// </summary>
internal sealed partial class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

        LogApplyingMigrations(logger);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying pending database migrations")]
    private static partial void LogApplyingMigrations(ILogger logger);
}
