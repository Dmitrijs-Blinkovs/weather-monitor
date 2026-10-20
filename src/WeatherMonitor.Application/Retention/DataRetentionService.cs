using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherMonitor.Application.Abstractions;

namespace WeatherMonitor.Application.Retention;

/// <summary>Deletes history older than the retention period, in batches.</summary>
public interface IDataRetentionService
{
    /// <returns>The number of fetch attempts deleted (their snapshots go with them).</returns>
    Task<int> PurgeAsync(CancellationToken cancellationToken);
}

internal sealed partial class DataRetentionService(
    IFetchAttemptRepository repository,
    IOptions<DataRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<DataRetentionService> logger) : IDataRetentionService
{
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.Days == 0)
        {
            LogRetentionDisabled(logger);
            return 0;
        }

        var cutoffUtc = timeProvider.GetUtcNow().UtcDateTime.AddDays(-settings.Days);
        var total = 0;
        int deleted;
        do
        {
            deleted = await repository.DeleteOlderThanAsync(cutoffUtc, settings.BatchSize, cancellationToken);
            total += deleted;
        }
        while (deleted == settings.BatchSize);

        LogPurged(logger, total, cutoffUtc);
        return total;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Data retention purged {Deleted} fetch attempts older than {CutoffUtc:O}")]
    private static partial void LogPurged(ILogger logger, int deleted, DateTime cutoffUtc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Data retention is disabled (Retention:Days = 0)")]
    private static partial void LogRetentionDisabled(ILogger logger);
}
