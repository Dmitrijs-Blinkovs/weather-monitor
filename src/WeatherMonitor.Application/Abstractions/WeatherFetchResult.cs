using System.Diagnostics.CodeAnalysis;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Abstractions;

/// <summary>Outcome of a single <see cref="IWeatherProvider"/> call.</summary>
public sealed class WeatherFetchResult
{
    private WeatherFetchResult(int? httpStatusCode, CurrentWeather? weather, string? error)
    {
        HttpStatusCode = httpStatusCode;
        Weather = weather;
        Error = error;
    }

    public int? HttpStatusCode { get; }

    public CurrentWeather? Weather { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Weather))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Weather is not null;

    public static WeatherFetchResult Success(int? httpStatusCode, CurrentWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        return new WeatherFetchResult(httpStatusCode, weather, error: null);
    }

    public static WeatherFetchResult Failure(int? httpStatusCode, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new WeatherFetchResult(httpStatusCode, weather: null, error);
    }
}
