namespace WeatherMonitor.Domain;

/// <summary>
/// One attempt to fetch the weather for a city. A successful attempt owns the <see cref="WeatherSnapshot"/> it produced.
/// </summary>
public sealed class FetchAttempt
{
    public const int CityMaxLength = 100;
    public const int ErrorMessageMaxLength = 2000;

    /// <summary>
    /// Longest response body kept for a failed attempt: the same limit as a successful payload, which the HTTP client
    /// also enforces, so a real provider body is never cut. Only a provider without such a cap could reach it.
    /// </summary>
    public const int ResponseBodyMaxLength = CurrentWeather.RawPayloadMaxLength;

    // Used by EF Core when materializing from the database.
    private FetchAttempt()
    {
    }

    private FetchAttempt(string city, DateTime attemptedAtUtc, TimeSpan duration, bool isSuccess, int? httpStatusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(city.Length, CityMaxLength, nameof(city));
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        if (httpStatusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(httpStatusCode), httpStatusCode, "Not an HTTP status code.");
        }

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

    /// <summary>
    /// The provider's response body of a failed attempt, as received (with the API key redacted by the adapter), so a
    /// changed or broken response can be diagnosed later. A successful attempt keeps its payload in the snapshot.
    /// </summary>
    public string? ResponseBody { get; private set; }

    public WeatherSnapshot? Snapshot { get; private set; }

    public static FetchAttempt Succeeded(
        string city,
        DateTime attemptedAtUtc,
        TimeSpan duration,
        int? httpStatusCode,
        CurrentWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        if (httpStatusCode is not (null or (>= 200 and <= 299)))
        {
            throw new ArgumentException(
                $"A successful attempt cannot have HTTP status {httpStatusCode}.", nameof(httpStatusCode));
        }

        var attempt = new FetchAttempt(city, attemptedAtUtc, duration, isSuccess: true, httpStatusCode);
        attempt.Snapshot = new WeatherSnapshot(attempt, weather);
        return attempt;
    }

    public static FetchAttempt Failed(
        string city,
        DateTime attemptedAtUtc,
        TimeSpan duration,
        int? httpStatusCode,
        string errorMessage,
        string? responseBody = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new FetchAttempt(city, attemptedAtUtc, duration, isSuccess: false, httpStatusCode)
        {
            ErrorMessage = SanitizeErrorMessage(errorMessage),
            ResponseBody = string.IsNullOrWhiteSpace(responseBody) ? null
                : responseBody.Length <= ResponseBodyMaxLength ? responseBody
                : responseBody[..ResponseBodyMaxLength],
        };
    }

    /// <summary>
    /// Error texts come from third parties and are shown publicly: control characters (line breaks could forge lines
    /// in plain-text logs) become spaces, and the text is cut to the column length.
    /// </summary>
    internal static string SanitizeErrorMessage(string errorMessage)
    {
        var cleaned = string.Create(
            errorMessage.Length,
            errorMessage,
            static (target, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    target[i] = char.IsControl(source[i]) ? ' ' : source[i];
                }
            }).Trim();

        if (cleaned.Length == 0)
        {
            return "Unknown error";
        }

        return cleaned.Length <= ErrorMessageMaxLength ? cleaned : cleaned[..ErrorMessageMaxLength];
    }

    internal static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException("Timestamps must be UTC.", nameof(value)),
    };
}
