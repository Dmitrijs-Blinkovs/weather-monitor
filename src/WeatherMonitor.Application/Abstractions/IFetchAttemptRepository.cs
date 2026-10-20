using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Abstractions;

/// <summary>Persists fetch attempts together with the snapshots they produced.</summary>
public interface IFetchAttemptRepository
{
    /// <summary>Stores all attempts (and their snapshots) in a single transaction.</summary>
    Task AddRangeAsync(IReadOnlyCollection<FetchAttempt> attempts, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> attempts made before <paramref name="cutoffUtc"/>, together with
    /// their snapshots, and returns how many attempts were deleted.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken);
}
