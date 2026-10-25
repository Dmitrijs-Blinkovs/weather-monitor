using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Reports Degraded when a city that is being fetched has had no successful fetch recently, so a stalled ingestion
/// (expired key, broken timer, one city failing) shows up in health probes and in the deployment smoke test instead of
/// only as missing points on the chart. The cities checked are those attempted within the last day: a city removed from
/// the configuration stops counting after that.
/// </summary>
internal sealed class DataFreshnessHealthCheck(WeatherDbContext dbContext, TimeProvider timeProvider) : IHealthCheck
{
    /// <summary>The timer runs every minute; ten minutes without a success means something is wrong.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    /// <summary>How far back a city's attempts make it a city that is expected to be fresh.</summary>
    public static readonly TimeSpan ExpectedCityWindow = TimeSpan.FromDays(1);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var sinceUtc = nowUtc - ExpectedCityWindow;

        // One row per city attempted in the window, with its latest success (null when it only failed).
        var cities = await dbContext.FetchAttempts
            .AsNoTracking()
            .Where(attempt => attempt.AttemptedAtUtc >= sinceUtc)
            .GroupBy(attempt => attempt.City)
            .Select(city => new
            {
                City = city.Key,
                LatestSuccessUtc = city.Max(attempt => attempt.IsSuccess ? (DateTime?)attempt.AttemptedAtUtc : null),
            })
            .ToListAsync(cancellationToken);

        if (cities.Count == 0)
        {
            return HealthCheckResult.Degraded("No weather fetch has been stored in the last day.");
        }

        var stale = cities
            .Where(city => city.LatestSuccessUtc is null || nowUtc - city.LatestSuccessUtc.Value > MaxAge)
            .OrderBy(city => city.City, StringComparer.OrdinalIgnoreCase)
            .Select(city => city.LatestSuccessUtc is { } latest
                ? $"{city.City} (last success {Minutes(nowUtc - latest)} min ago)"
                : $"{city.City} (no success in the last day)")
            .ToList();

        return stale.Count == 0
            ? HealthCheckResult.Healthy($"Every city ({cities.Count}) fetched successfully within {Minutes(MaxAge)} min.")
            : HealthCheckResult.Degraded($"No recent successful fetch for: {string.Join(", ", stale)}.");
    }

    private static string Minutes(TimeSpan age) => age.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
}
