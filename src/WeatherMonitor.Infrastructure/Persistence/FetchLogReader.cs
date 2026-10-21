using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.Paging;

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

        // Deferred join: OFFSET runs over the narrow index keys only (time + Id), then the full rows of just this page
        // are read. Skipping full rows instead costs a key lookup per skipped row: page 1,000 x 200 read ~640,000
        // pages that way on 90 days of data. The subquery stays inside the lambda so that skip and take are sent as
        // SQL parameters (a pre-built IQueryable would bake them in as constants: one cached plan per page).
        var skip = query.Skip;
        var take = query.PageSize;
        var items = await dbContext.FetchAttempts
            .AsNoTracking()
            .Where(attempt => attempts
                .OrderByDescending(candidate => candidate.AttemptedAtUtc)
                .ThenByDescending(candidate => candidate.Id)
                .Skip(skip)
                .Take(take)
                .Select(candidate => candidate.Id)
                .Contains(attempt.Id))
            .OrderByDescending(attempt => attempt.AttemptedAtUtc)
            .ThenByDescending(attempt => attempt.Id)
            .Select(attempt => new FetchLogEntryDto(
                attempt.Id,
                attempt.City,
                attempt.AttemptedAtUtc,
                attempt.IsSuccess,
                attempt.HttpStatusCode,
                attempt.DurationMs))
            .ToListAsync(cancellationToken);

        return new PagedResult<FetchLogEntryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<FetchAttemptDetailsDto?> GetDetailsAsync(long id, CancellationToken cancellationToken) =>
        dbContext.FetchAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Id == id)
            .Select(attempt => new FetchAttemptDetailsDto(
                attempt.Id,
                attempt.City,
                attempt.AttemptedAtUtc,
                attempt.IsSuccess,
                attempt.HttpStatusCode,
                attempt.ErrorMessage,
                attempt.DurationMs,
                // A success keeps its payload in the snapshot, a failure the body it got on the attempt itself.
                attempt.IsSuccess ? attempt.Snapshot!.RawPayload : attempt.ResponseBody))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken cancellationToken) =>
        await dbContext.FetchAttempts
            .AsNoTracking()
            .Select(attempt => attempt.City)
            .Distinct()
            .OrderBy(city => city)
            .ToListAsync(cancellationToken);
}
