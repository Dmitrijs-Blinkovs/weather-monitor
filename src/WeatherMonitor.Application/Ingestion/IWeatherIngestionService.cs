namespace WeatherMonitor.Application.Ingestion;

/// <summary>Fetches the weather for every configured city and stores the outcome of each attempt.</summary>
public interface IWeatherIngestionService
{
    Task<IngestionSummary> IngestAsync(CancellationToken cancellationToken);
}

public sealed record IngestionSummary(int Succeeded, int Failed);
