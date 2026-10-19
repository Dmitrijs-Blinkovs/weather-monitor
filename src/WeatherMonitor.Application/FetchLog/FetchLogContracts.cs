namespace WeatherMonitor.Application.FetchLog;

public enum FetchStatus
{
    Success,
    Failure,
}

/// <param name="Page">1-based page number.</param>
public sealed record FetchLogQuery(int Page, int PageSize, string? City = null, FetchStatus? Status = null)
{
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
