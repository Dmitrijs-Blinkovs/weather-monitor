using Microsoft.Azure.Functions.Worker;
using WeatherMonitor.Application.Retention;

namespace WeatherMonitor.Functions;

/// <summary>Daily purge of history older than <c>Retention:Days</c>; all the work is in <see cref="IDataRetentionService"/>.</summary>
public sealed class PurgeOldDataFunction(IDataRetentionService retentionService)
{
    /// <summary>NCRONTAB (with seconds): every day at 03:30 UTC, a quiet hour for the database.</summary>
    public const string DailyAtHalfPastThree = "0 30 3 * * *";

    [Function("PurgeOldData")]
    public async Task RunAsync([TimerTrigger(DailyAtHalfPastThree)] TimerInfo timer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);
        await retentionService.PurgeAsync(cancellationToken);
    }
}
