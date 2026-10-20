using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Reports Degraded when no fetch has succeeded recently, so a stalled ingestion (expired key, broken timer) shows up
/// in health probes and in the deployment smoke test instead of only as missing points on the chart.
/// </summary>
internal sealed class DataFreshnessHealthCheck(WeatherDbContext dbContext, TimeProvider timeProvider) : IHealthCheck
{
    /// <summary>The timer runs every minute; ten minutes without a success means something is wrong.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var latestSuccessUtc = await dbContext.FetchAttempts
            .AsNoTracking()
            .Where(attempt => attempt.IsSuccess)
            .OrderByDescending(attempt => attempt.AttemptedAtUtc)
            .Select(attempt => (DateTime?)attempt.AttemptedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSuccessUtc is null)
        {
            return HealthCheckResult.Degraded("No weather data has been stored yet.");
        }

        var age = timeProvider.GetUtcNow().UtcDateTime - latestSuccessUtc.Value;
        var minutes = age.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return age <= MaxAge
            ? HealthCheckResult.Healthy($"Last successful fetch {minutes} min ago.")
            : HealthCheckResult.Degraded($"No successful fetch for {minutes} min.");
    }
}
