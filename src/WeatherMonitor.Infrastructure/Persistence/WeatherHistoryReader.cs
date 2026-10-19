using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Infrastructure.Persistence;

internal sealed class WeatherHistoryReader(WeatherDbContext dbContext) : IWeatherHistoryReader
{
    public async Task<IReadOnlyList<WeatherObservation>> GetObservationsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        // The provider refreshes an observation roughly every 10 minutes while we fetch every minute,
        // so DISTINCT collapses repeated snapshots of the same observation into a single point.
        var rows = await dbContext.WeatherSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ObservedAtUtc >= fromUtc && snapshot.ObservedAtUtc <= toUtc)
            .Select(snapshot => new
            {
                snapshot.City,
                snapshot.Country,
                snapshot.Temperature,
                snapshot.TemperatureMin,
                snapshot.TemperatureMax,
                snapshot.ObservedAtUtc,
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new WeatherObservation(
                row.City,
                row.Country,
                row.Temperature,
                row.TemperatureMin,
                row.TemperatureMax,
                row.ObservedAtUtc))
            .ToList();
    }
}
