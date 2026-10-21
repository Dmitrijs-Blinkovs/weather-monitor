using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Application.FetchLog;

/// <summary>Read side of the fetch attempt log.</summary>
public interface IFetchLogReader
{
    /// <summary>Returns one page of attempts, newest first.</summary>
    Task<PagedResult<FetchLogEntryDto>> GetPageAsync(FetchLogQuery query, CancellationToken cancellationToken);

    /// <summary>Returns one attempt with its response body, or null when it does not exist (or was purged).</summary>
    Task<FetchAttemptDetailsDto?> GetDetailsAsync(long id, CancellationToken cancellationToken);

    /// <summary>Returns every city that has at least one fetch attempt, alphabetically.</summary>
    Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken cancellationToken);
}
