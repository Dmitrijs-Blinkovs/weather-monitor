using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Ingestion;

/// <summary>Which cities are fetched on every run. Bound from the "Weather" configuration section.</summary>
public sealed partial class WeatherIngestionOptions : IValidatableObject
{
    public const string SectionName = "Weather";

    [MinLength(1, ErrorMessage = "Configure at least one city in Weather:Cities.")]
    public List<CityOptions> Cities { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Cities.Any(city => string.IsNullOrWhiteSpace(city.Name)))
        {
            yield return new ValidationResult("City names must not be empty.", [nameof(Cities)]);
        }

        if (Cities.Any(city => city.Name?.Length > FetchAttempt.CityMaxLength))
        {
            yield return new ValidationResult(
                $"City names must be at most {FetchAttempt.CityMaxLength} characters long.", [nameof(Cities)]);
        }

        if (Cities.Select(city => city.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Cities.Count)
        {
            yield return new ValidationResult("City names must be unique.", [nameof(Cities)]);
        }

        if (Cities.Any(city => city.CountryCode is not null && !CountryCodePattern().IsMatch(city.CountryCode)))
        {
            yield return new ValidationResult(
                "Country codes must be two-letter ISO 3166 codes, e.g. \"IT\".", [nameof(Cities)]);
        }
    }

    [GeneratedRegex("^[A-Za-z]{2}$")]
    private static partial Regex CountryCodePattern();
}

/// <summary>
/// A city to fetch. The country code disambiguates the name for the provider
/// ("Rome" alone resolves to Rome, Georgia, USA); the name is what logs and charts show.
/// </summary>
public sealed class CityOptions
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional ISO 3166 alpha-2 country code, e.g. "IT".</summary>
    public string? CountryCode { get; init; }
}
