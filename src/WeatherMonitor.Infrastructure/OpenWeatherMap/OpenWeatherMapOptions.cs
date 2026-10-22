using System.ComponentModel.DataAnnotations;

namespace WeatherMonitor.Infrastructure.OpenWeatherMap;

/// <summary>Bound from the "OpenWeatherMap" configuration section.</summary>
public sealed class OpenWeatherMapOptions : IValidatableObject
{
    public const string SectionName = "OpenWeatherMap";

    [Required]
    public string BaseUrl { get; init; } = "https://api.openweathermap.org/";

    [Required(ErrorMessage =
        "OpenWeatherMap:ApiKey is not configured. Locally, set it with 'dotnet user-secrets' (see README).")]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// The whole time one city's request may take: retries, the delays between them and reading the response body
    /// included. Each attempt is capped at a third of it, so fewer than three may fit when attempts or delays run long.
    /// At most 40 s, so fetching plus the 10 s save stay below the Functions timeout
    /// of 55 s.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:00:40")]
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // The API key travels in the query string, so it must never be sent over plain HTTP.
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "OpenWeatherMap:BaseUrl must be an absolute https:// URL.", [nameof(BaseUrl)]);
        }
    }
}
