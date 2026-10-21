using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Infrastructure.Persistence;

internal sealed class WeatherReadingsReader(WeatherDbContext dbContext) : IWeatherReadingsReader
{
    public async Task<PagedResult<WeatherReadingDto>> GetPageAsync(
        WeatherReadingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Every successful attempt owns exactly one snapshot, so the attempts' (IsSuccess, AttemptedAtUtc) index counts
        // and pages the readings; unlike the chart history, nothing is collapsed here.
        var fromUtc = query.FromUtc;
        var toUtc = query.ToUtc;
        var attempts = dbContext.FetchAttempts
            .AsNoTracking()
            .Where(attempt => attempt.IsSuccess && attempt.AttemptedAtUtc >= fromUtc && attempt.AttemptedAtUtc <= toUtc);

        var totalCount = await attempts.CountAsync(cancellationToken);

        // Deferred join, as in FetchLogReader: OFFSET runs over the index keys, then only this page's snapshots are read
        // (through their unique FetchAttemptId index), never the payload. The subquery stays inside the lambda so that
        // skip and take are sent as SQL parameters.
        var skip = query.Skip;
        var take = query.PageSize;
        var items = await dbContext.WeatherSnapshots
            .AsNoTracking()
            .Where(snapshot => attempts
                .OrderByDescending(candidate => candidate.AttemptedAtUtc)
                .ThenByDescending(candidate => candidate.Id)
                .Skip(skip)
                .Take(take)
                .Select(candidate => candidate.Id)
                .Contains(snapshot.FetchAttemptId))
            .OrderByDescending(snapshot => snapshot.FetchedAtUtc)
            .ThenByDescending(snapshot => snapshot.FetchAttemptId)
            .Select(snapshot => new WeatherReadingDto(
                snapshot.FetchAttemptId,
                snapshot.FetchedAtUtc,
                snapshot.City,
                snapshot.Country,
                snapshot.Temperature,
                snapshot.TemperatureMin,
                snapshot.TemperatureMax,
                snapshot.ObservedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<WeatherReadingDto>(items, query.Page, query.PageSize, totalCount);
    }
}
