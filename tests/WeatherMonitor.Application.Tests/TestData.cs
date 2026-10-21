using WeatherMonitor.Application.History;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    public static CurrentWeather Weather(
        double temperature = 11.5,
        string country = "GB") =>
        new(
            country: country,
            temperature: temperature,
            temperatureMin: temperature - 1,
            temperatureMax: temperature + 1,
            observedAtUtc: Now.AddMinutes(-5),
            rawPayload: """{"name":"Test","main":{"temp":11.5}}""");

    public static WeatherObservation Observation(
        string city,
        DateTime observedAtUtc,
        double temperature,
        double? min = null,
        double? max = null,
        string country = "GB",
        long latestSnapshotId = 1) =>
        new(city, country, temperature, min ?? temperature - 1, max ?? temperature + 1, observedAtUtc, latestSnapshotId);
}
