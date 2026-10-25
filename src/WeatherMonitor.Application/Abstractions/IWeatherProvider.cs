namespace WeatherMonitor.Application.Abstractions;

/// <summary>Source of current weather data (implemented by the OpenWeatherMap client).</summary>
public interface IWeatherProvider
{
    /// <summary>
    /// Fetches the current weather for a city. Expected failures (HTTP errors, timeouts, invalid payloads)
    /// are returned as a failed <see cref="WeatherFetchResult"/> rather than thrown.
    /// </summary>
    /// <param name="city">City name, e.g. "Rome".</param>
    /// <param name="countryCode">Optional ISO 3166 country code that disambiguates the name, e.g. "IT".</param>
    Task<WeatherFetchResult> GetCurrentWeatherAsync(
        string city,
        string? countryCode,
        CancellationToken cancellationToken);
}
