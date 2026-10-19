namespace WeatherMonitor.Application.History;

/// <summary>One distinct weather observation, as read from storage.</summary>
public sealed record WeatherObservation(
    string City,
    string Country,
    double Temperature,
    double TemperatureMin,
    double TemperatureMax,
    DateTime ObservedAtUtc);

public sealed record TemperaturePointDto(
    DateTime ObservedAtUtc,
    double Temperature,
    double TemperatureMin,
    double TemperatureMax);

/// <summary>Temperature series for one city plus a summary of the requested period.</summary>
/// <param name="MinTemperature">Lowest <c>temp_min</c> observed in the period.</param>
/// <param name="MaxTemperature">Highest <c>temp_max</c> observed in the period.</param>
public sealed record CityTemperatureSeriesDto(
    string City,
    string Country,
    double LatestTemperature,
    double MinTemperature,
    double MaxTemperature,
    DateTime LastUpdatedUtc,
    IReadOnlyList<TemperaturePointDto> Points);

public sealed record WeatherHistoryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<CityTemperatureSeriesDto> Cities);
