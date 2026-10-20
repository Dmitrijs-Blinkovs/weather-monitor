namespace WeatherMonitor.Application.FetchLog;

public enum FetchStatus
{
    Success,
    Failure,
}

/// <summary>One page of the fetch log. The limits are business rules; the API validates against the same constants.</summary>
public sealed record FetchLogQuery
{
    public const int MaxPageSize = 200;

    /// <summary>
    /// Deepest page that can be requested. Deep OFFSET paging costs the database more with every page; older entries
    /// are reached by filtering (city, result) instead. Must match MAX_LOG_PAGE in the client.
    /// </summary>
    public const int MaxPage = 1000;

    public FetchLogQuery(int page, int pageSize, string? city = null, FetchStatus? status = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, MaxPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        Page = page;
        PageSize = pageSize;
        City = city;
        Status = status;
    }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    public int PageSize { get; }

    public string? City { get; }

    public FetchStatus? Status { get; }

    public int Skip => (Page - 1) * PageSize;
}

public sealed record FetchLogEntryDto(
    long Id,
    string City,
    DateTime AttemptedAtUtc,
    bool IsSuccess,
    int? HttpStatusCode,
    string? ErrorMessage,
    int DurationMs);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
