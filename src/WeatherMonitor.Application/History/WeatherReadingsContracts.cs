using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Application.History;

/// <summary>
/// One page of weather readings: every successful fetch within a time range, newest first. Unlike the chart history,
/// repeated fetches of the same provider observation are all listed, so every response the function stored is visible.
/// </summary>
public sealed record WeatherReadingsQuery
{
    /// <exception cref="ArgumentException">The range violates <see cref="WeatherHistoryRules"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page or its size is outside <see cref="PageLimits"/>.</exception>
    public WeatherReadingsQuery(DateTime fromUtc, DateTime toUtc, int page, int pageSize)
    {
        var error = WeatherHistoryRules.Validate(fromUtc, toUtc).FirstOrDefault();
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(fromUtc));
        }

        PageLimits.ThrowIfInvalid(page, pageSize);

        FromUtc = fromUtc;
        ToUtc = toUtc;
        Page = page;
        PageSize = pageSize;
    }

    public DateTime FromUtc { get; }

    public DateTime ToUtc { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>The weather one successful fetch returned.</summary>
/// <param name="Id">Id of the fetch attempt, as in the fetch log.</param>
/// <param name="FetchedAtUtc">
/// When the fetch was made: the attempt's start; the response arrived within the attempt's duration.
/// </param>
/// <param name="ObservedAtUtc">
/// The provider's "last update" time of the observation; it repeats until the provider refreshes the observation.
/// </param>
public sealed record WeatherReadingDto(
    long Id,
    DateTime FetchedAtUtc,
    string City,
    string Country,
    double Temperature,
    double TemperatureMin,
    double TemperatureMax,
    DateTime ObservedAtUtc);
