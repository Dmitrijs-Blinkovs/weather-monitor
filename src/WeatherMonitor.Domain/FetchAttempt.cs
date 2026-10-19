namespace WeatherMonitor.Domain;

/// <summary>
/// One attempt to fetch the weather for a city. A successful attempt owns the <see cref="WeatherSnapshot"/> it produced.
/// </summary>
public sealed class FetchAttempt
{
    public const int CityMaxLength = 100;
    public const int ErrorMessageMaxLength = 2000;

    // Used by EF Core when materializing from the database.
    private FetchAttempt()
    {
    }

    private FetchAttempt(string city, DateTime attemptedAtUtc, TimeSpan duration, bool isSuccess, int? httpStatusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(city.Length, CityMaxLength, nameof(city));
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        City = city;
        AttemptedAtUtc = EnsureUtc(attemptedAtUtc);
        DurationMs = (int)Math.Min(duration.TotalMilliseconds, int.MaxValue);
        IsSuccess = isSuccess;
        HttpStatusCode = httpStatusCode;
    }

    public long Id { get; private set; }

    public string City { get; private set; } = string.Empty;

    public DateTime AttemptedAtUtc { get; private set; }

    public bool IsSuccess { get; private set; }

    public int? HttpStatusCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int DurationMs { get; private set; }

    public WeatherSnapshot? Snapshot { get; private set; }

    public static FetchAttempt Succeeded(
        string city,
        DateTime attemptedAtUtc,
        TimeSpan duration,
        int? httpStatusCode,
        CurrentWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);

        var attempt = new FetchAttempt(city, attemptedAtUtc, duration, isSuccess: true, httpStatusCode);
        attempt.Snapshot = new WeatherSnapshot(attempt, weather);
        return attempt;
    }

    public static FetchAttempt Failed(
        string city,
        DateTime attemptedAtUtc,
        TimeSpan duration,
        int? httpStatusCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new FetchAttempt(city, attemptedAtUtc, duration, isSuccess: false, httpStatusCode)
        {
            ErrorMessage = errorMessage.Length <= ErrorMessageMaxLength
                ? errorMessage
                : errorMessage[..ErrorMessageMaxLength],
        };
    }

    internal static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException("Timestamps must be UTC.", nameof(value)),
    };
}
