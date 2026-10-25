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
        // The provider refreshes an observation roughly every 10 minutes while we fetch every minute, so grouping by
        // the observation's values collapses repeated snapshots into a single point. MAX(Id) marks the version stored
        // last when the provider revised values under the same time; Id is the clustered key, so the covering index
        // on (ObservedAtUtc, City) still answers the query without reading the rows.
        var rows = await dbContext.WeatherSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ObservedAtUtc >= fromUtc && snapshot.ObservedAtUtc <= toUtc)
            .GroupBy(snapshot => new
            {
                snapshot.City,
                snapshot.Country,
                snapshot.Temperature,
                snapshot.TemperatureMin,
                snapshot.TemperatureMax,
                snapshot.ObservedAtUtc,
            })
            .Select(group => new { group.Key, LatestSnapshotId = group.Max(snapshot => snapshot.Id) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new WeatherObservation(
                row.Key.City,
                row.Key.Country,
                row.Key.Temperature,
                row.Key.TemperatureMin,
                row.Key.TemperatureMax,
                row.Key.ObservedAtUtc,
                row.LatestSnapshotId))
            .ToList();
    }
}
