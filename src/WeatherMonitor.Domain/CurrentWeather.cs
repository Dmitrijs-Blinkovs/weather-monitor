using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WeatherMonitor.Domain;

/// <summary>
/// Current weather for one city as reported by a weather provider. Temperatures are in degrees Celsius.
/// A value object that cannot exist in an invalid state, whichever provider created it: providers call
/// <see cref="TryCreate"/> to turn bad data into a failed fetch, and the constructor rejects it as a last line of defence.
/// </summary>
public sealed record CurrentWeather
{
    /// <summary>ISO 3166-1 alpha-2: exactly two letters A-Z.</summary>
    public const int CountryCodeLength = 2;

    /// <summary>
    /// Largest payload kept (a weather response is about 500 characters). The HTTP client refuses larger responses
    /// with the same number of bytes, and a larger payload is rejected here rather than stored partially.
    /// </summary>
    public const int RawPayloadMaxLength = 64 * 1024;

    /// <summary>Plausibility bounds for air temperature (the records are about -90 and +57 degrees Celsius).</summary>
    public const double MinPlausibleCelsius = -100;

    public const double MaxPlausibleCelsius = 70;

    public CurrentWeather(
        string country,
        double temperature,
        double temperatureMin,
        double temperatureMax,
        DateTime observedAtUtc,
        string rawPayload)
    {
        var violation = FindViolation(country, temperature, temperatureMin, temperatureMax, observedAtUtc, rawPayload);
        if (violation is not null)
        {
            throw new ArgumentException(violation);
        }

        Country = country.ToUpperInvariant();
        Temperature = temperature;
        TemperatureMin = temperatureMin;
        TemperatureMax = temperatureMax;
        ObservedAtUtc = FetchAttempt.EnsureUtc(observedAtUtc);
        RawPayload = rawPayload;
    }

    /// <summary>ISO 3166-1 alpha-2 country code in upper case, e.g. "GB".</summary>
    public string Country { get; }

    public double Temperature { get; }

    /// <summary>Minimum temperature currently observed within the city area (not the daily minimum).</summary>
    public double TemperatureMin { get; }

    /// <summary>Maximum temperature currently observed within the city area (not the daily maximum).</summary>
    public double TemperatureMax { get; }

    /// <summary>Time the provider last updated this observation.</summary>
    public DateTime ObservedAtUtc { get; }

    /// <summary>The provider's full response body, stored as received.</summary>
    public string RawPayload { get; }

    /// <summary>Creates the observation, or returns why the values cannot form one.</summary>
    public static bool TryCreate(
        string country,
        double temperature,
        double temperatureMin,
        double temperatureMax,
        DateTime observedAtUtc,
        string rawPayload,
        [NotNullWhen(true)] out CurrentWeather? weather,
        [NotNullWhen(false)] out string? error)
    {
        error = FindViolation(country, temperature, temperatureMin, temperatureMax, observedAtUtc, rawPayload);
        weather = error is null
            ? new CurrentWeather(country, temperature, temperatureMin, temperatureMax, observedAtUtc, rawPayload)
            : null;
        return weather is not null;
    }

    private static string? FindViolation(
        string country,
        double temperature,
        double temperatureMin,
        double temperatureMax,
        DateTime observedAtUtc,
        string rawPayload)
    {
        if (country is not { Length: CountryCodeLength } || !country.All(char.IsAsciiLetter))
        {
            return $"The country must be a two-letter ISO 3166 code, not '{country}'.";
        }

        if (!IsPlausible(temperature) || !IsPlausible(temperatureMin) || !IsPlausible(temperatureMax)
            || temperatureMin > temperatureMax)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Implausible temperatures: temp {temperature}, temp_min {temperatureMin}, temp_max {temperatureMax}");
        }

        if (observedAtUtc.Kind == DateTimeKind.Unspecified)
        {
            return "The observation time must be UTC; its time zone is unknown.";
        }

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return "The raw payload is empty.";
        }

        return rawPayload.Length > RawPayloadMaxLength
            ? $"The raw payload has {rawPayload.Length} characters; at most {RawPayloadMaxLength} are kept."
            : null;
    }

    private static bool IsPlausible(double celsius) =>
        double.IsFinite(celsius) && celsius is >= MinPlausibleCelsius and <= MaxPlausibleCelsius;
}
