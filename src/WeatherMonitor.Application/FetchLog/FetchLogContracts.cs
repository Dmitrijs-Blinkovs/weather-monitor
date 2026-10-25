using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Application.FetchLog;

public enum FetchStatus
{
    Success,
    Failure,
}

/// <summary>One page of the fetch log, within <see cref="PageLimits"/>.</summary>
public sealed record FetchLogQuery
{
    public FetchLogQuery(int page, int pageSize, string? city = null, FetchStatus? status = null)
    {
        PageLimits.ThrowIfInvalid(page, pageSize);

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

/// <summary>One row of the log. The error text and the response body are loaded per attempt (<see cref="FetchAttemptDetailsDto"/>).</summary>
public sealed record FetchLogEntryDto(
    long Id,
    string City,
    DateTime AttemptedAtUtc,
    bool IsSuccess,
    int? HttpStatusCode,
    int DurationMs);

/// <summary>One attempt with everything that was stored for it, loaded on demand (the log list never carries bodies).</summary>
/// <param name="ResponseBody">
/// The provider's response as received: the full payload of a successful attempt, or the body of a failed one (with
/// the API key redacted); null when no response arrived (timeout, network error).
/// </param>
public sealed record FetchAttemptDetailsDto(
    long Id,
    string City,
    DateTime AttemptedAtUtc,
    bool IsSuccess,
    int? HttpStatusCode,
    string? ErrorMessage,
    int DurationMs,
    string? ResponseBody);
