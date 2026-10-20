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
