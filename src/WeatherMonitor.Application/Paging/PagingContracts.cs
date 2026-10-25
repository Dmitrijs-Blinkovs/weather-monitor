namespace WeatherMonitor.Application.Paging;

/// <summary>Limits of every paged list. They are business rules; the API validates against the same constants.</summary>
public static class PageLimits
{
    public const int MaxPageSize = 200;

    /// <summary>
    /// Deepest page that can be requested. Deep OFFSET paging costs the database more with every page; older rows are
    /// reached by narrowing the list (a filter or a time range) instead. Must match MAX_PAGE in the client.
    /// </summary>
    public const int MaxPage = 1000;

    /// <exception cref="ArgumentOutOfRangeException">The page or its size is outside the limits.</exception>
    public static void ThrowIfInvalid(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, MaxPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
