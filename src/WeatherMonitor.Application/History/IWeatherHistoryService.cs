namespace WeatherMonitor.Application.History;

/// <summary>Builds the temperature history shown on the chart page.</summary>
public interface IWeatherHistoryService
{
    /// <exception cref="ArgumentException">The range violates <see cref="WeatherHistoryRules"/>.</exception>
    Task<WeatherHistoryDto> GetHistoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
