using System.Text.Json.Serialization;

namespace WeatherMonitor.Infrastructure.OpenWeatherMap;

// Only the fields we extract; the full body is stored verbatim as the raw payload.
// See https://openweathermap.org/current#fields_json
internal sealed record OpenWeatherMapResponse(
    [property: JsonPropertyName("dt")] long? Dt,
    [property: JsonPropertyName("main")] OpenWeatherMapMain? Main,
    [property: JsonPropertyName("sys")] OpenWeatherMapSys? Sys);

internal sealed record OpenWeatherMapMain(
    [property: JsonPropertyName("temp")] double? Temp,
    [property: JsonPropertyName("temp_min")] double? TempMin,
    [property: JsonPropertyName("temp_max")] double? TempMax);

internal sealed record OpenWeatherMapSys(
    [property: JsonPropertyName("country")] string? Country);

internal sealed record OpenWeatherMapError(
    [property: JsonPropertyName("message")] string? Message);
