namespace WeatherMonitor.Domain;

/// <summary>
/// Current weather for one city as reported by a weather provider. Temperatures are in degrees Celsius.
/// </summary>
/// <param name="Country">ISO 3166 country code, e.g. "GB".</param>
/// <param name="TemperatureMin">
/// Minimum temperature currently observed within the city area (not the daily minimum).
/// </param>
/// <param name="TemperatureMax">
/// Maximum temperature currently observed within the city area (not the daily maximum).
/// </param>
/// <param name="ObservedAtUtc">Time the provider last updated this observation.</param>
/// <param name="RawPayload">The provider's full response body, stored as received.</param>
public sealed record CurrentWeather(
    string Country,
    double Temperature,
    double TemperatureMin,
    double TemperatureMax,
    DateTime ObservedAtUtc,
    string RawPayload);
