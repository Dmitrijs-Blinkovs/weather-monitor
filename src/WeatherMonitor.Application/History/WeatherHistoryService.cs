namespace WeatherMonitor.Application.History;

internal sealed class WeatherHistoryService(IWeatherHistoryReader reader) : IWeatherHistoryService
{
    public async Task<WeatherHistoryDto> GetHistoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var error = WeatherHistoryRules.Validate(fromUtc, toUtc).FirstOrDefault();
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(fromUtc));
        }

        var observations = await reader.GetObservationsAsync(fromUtc, toUtc, cancellationToken);
        return new WeatherHistoryDto(fromUtc, toUtc, BuildSeries(observations));
    }

    internal static IReadOnlyList<CityTemperatureSeriesDto> BuildSeries(IEnumerable<WeatherObservation> observations) =>
        observations
            .GroupBy(observation => observation.City, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCitySeries)
            .OrderBy(series => series.City, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static CityTemperatureSeriesDto BuildCitySeries(IGrouping<string, WeatherObservation> city)
    {
        var ordered = city.OrderBy(observation => observation.ObservedAtUtc).ToList();
        var latest = ordered[^1];

        return new CityTemperatureSeriesDto(
            City: city.Key,
            Country: latest.Country,
            LatestTemperature: latest.Temperature,
            MinTemperature: ordered.Min(observation => observation.TemperatureMin),
            MaxTemperature: ordered.Max(observation => observation.TemperatureMax),
            LastUpdatedUtc: latest.ObservedAtUtc,
            Points: ordered
                .Select(observation => new TemperaturePointDto(
                    observation.ObservedAtUtc,
                    observation.Temperature,
                    observation.TemperatureMin,
                    observation.TemperatureMax))
                .ToList());
    }
}
