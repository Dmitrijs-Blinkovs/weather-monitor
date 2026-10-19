namespace WeatherMonitor.Application.History;

/// <summary>Read side of the stored weather snapshots.</summary>
public interface IWeatherHistoryReader
{
    /// <summary>
    /// Returns the distinct observations whose observation time falls within the range (inclusive).
    /// Snapshots fetched repeatedly for the same provider observation are collapsed into one.
    /// </summary>
    Task<IReadOnlyList<WeatherObservation>> GetObservationsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}
