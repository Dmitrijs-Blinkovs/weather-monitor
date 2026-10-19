namespace WeatherMonitor.Domain;

/// <summary>
/// Weather data captured by a successful <see cref="FetchAttempt"/>: the full provider payload
/// plus the fields the UI queries, extracted so that charts never have to parse JSON.
/// </summary>
public sealed class WeatherSnapshot
{
    public const int CountryMaxLength = 8;

    // Used by EF Core when materializing from the database.
    private WeatherSnapshot()
    {
    }

    internal WeatherSnapshot(FetchAttempt attempt, CurrentWeather weather)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(weather.RawPayload);

        City = attempt.City;
        Country = weather.Country.Length <= CountryMaxLength ? weather.Country : weather.Country[..CountryMaxLength];
        Temperature = weather.Temperature;
        TemperatureMin = weather.TemperatureMin;
        TemperatureMax = weather.TemperatureMax;
        ObservedAtUtc = FetchAttempt.EnsureUtc(weather.ObservedAtUtc);
        FetchedAtUtc = attempt.AttemptedAtUtc;
        RawPayload = weather.RawPayload;
    }

    public long Id { get; private set; }

    public long FetchAttemptId { get; private set; }

    /// <summary>City name as configured for fetching (the key used across logs and charts).</summary>
    public string City { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public double Temperature { get; private set; }

    public double TemperatureMin { get; private set; }

    public double TemperatureMax { get; private set; }

    /// <summary>Time the provider last updated the observation ("last update time").</summary>
    public DateTime ObservedAtUtc { get; private set; }

    public DateTime FetchedAtUtc { get; private set; }

    /// <summary>Full JSON payload returned by the provider.</summary>
    public string RawPayload { get; private set; } = string.Empty;
}
