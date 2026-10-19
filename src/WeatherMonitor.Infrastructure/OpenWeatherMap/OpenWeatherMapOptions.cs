using System.ComponentModel.DataAnnotations;

namespace WeatherMonitor.Infrastructure.OpenWeatherMap;

/// <summary>Bound from the "OpenWeatherMap" configuration section.</summary>
public sealed class OpenWeatherMapOptions
{
    public const string SectionName = "OpenWeatherMap";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://api.openweathermap.org/";

    [Required(ErrorMessage =
        "OpenWeatherMap:ApiKey is not configured. Locally, set it with 'dotnet user-secrets' (see README).")]
    public string ApiKey { get; init; } = string.Empty;
}
