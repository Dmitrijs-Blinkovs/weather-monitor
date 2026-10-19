using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Application.FetchLog;

namespace WeatherMonitor.Infrastructure.Persistence;

internal sealed class FetchLogReader(WeatherDbContext dbContext) : IFetchLogReader
{
    public async Task<PagedResult<FetchLogEntryDto>> GetPageAsync(
        FetchLogQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var attempts = dbContext.FetchAttempts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            attempts = attempts.Where(attempt => attempt.City == query.City);
        }

        if (query.Status is { } status)
        {
            var isSuccess = status == FetchStatus.Success;
            attempts = attempts.Where(attempt => attempt.IsSuccess == isSuccess);
        }

        var totalCount = await attempts.CountAsync(cancellationToken);

        var items = await attempts
            .OrderByDescending(attempt => attempt.AttemptedAtUtc)
            .ThenByDescending(attempt => attempt.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(attempt => new FetchLogEntryDto(
                attempt.Id,
                attempt.City,
                attempt.AttemptedAtUtc,
                attempt.IsSuccess,
                attempt.HttpStatusCode,
                attempt.ErrorMessage,
                attempt.DurationMs))
            .ToListAsync(cancellationToken);

        return new PagedResult<FetchLogEntryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken cancellationToken) =>
        await dbContext.FetchAttempts
            .AsNoTracking()
            .Select(attempt => attempt.City)
            .Distinct()
            .OrderBy(city => city)
            .ToListAsync(cancellationToken);
}
