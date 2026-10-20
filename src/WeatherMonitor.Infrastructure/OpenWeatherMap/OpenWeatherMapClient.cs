using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.OpenWeatherMap;

/// <summary>
/// Calls the OpenWeatherMap "current weather" endpoint. Retries and timeouts are handled by the
/// resilience pipeline configured on the <see cref="HttpClient"/>; this class turns every outcome into a result.
/// </summary>
internal sealed class OpenWeatherMapClient(HttpClient httpClient, IOptions<OpenWeatherMapOptions> options)
    : IWeatherProvider
{
    private const string Redacted = "***";

    public async Task<WeatherFetchResult> GetCurrentWeatherAsync(
        string city,
        string? countryCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        // "{city},{country}" is the documented way to disambiguate a city name.
        var location = string.IsNullOrWhiteSpace(countryCode) ? city : $"{city},{countryCode}";
        var apiKey = options.Value.ApiKey;
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"data/2.5/weather?q={Uri.EscapeDataString(location)}&units=metric&appid={Uri.EscapeDataString(apiKey)}");

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            var statusCode = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = FirstNonBlank(ReadErrorMessage(body), response.ReasonPhrase) ?? $"HTTP {statusCode}";
                return WeatherFetchResult.Failure(statusCode, Redact(message, apiKey));
            }

            return TryParse(body, out var weather, out var parseError)
                ? WeatherFetchResult.Success(statusCode, weather)
                : WeatherFetchResult.Failure(statusCode, parseError);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = exception.StatusCode is { } code ? (int)code : (int?)null;
            return WeatherFetchResult.Failure(statusCode, Redact($"Request failed: {exception.Message}", apiKey));
        }
        catch (ExecutionRejectedException exception)
        {
            // Raised by the resilience pipeline: attempt/total timeout exceeded or circuit breaker open.
            return WeatherFetchResult.Failure(httpStatusCode: null, Redact(exception.Message, apiKey));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WeatherFetchResult.Failure(httpStatusCode: null, "Request timed out");
        }
    }

    internal static bool TryParse(
        string body,
        [NotNullWhen(true)] out CurrentWeather? weather,
        [NotNullWhen(false)] out string? error)
    {
        weather = null;

        OpenWeatherMapResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<OpenWeatherMapResponse>(body);
        }
        catch (JsonException)
        {
            error = "Response body is not valid JSON";
            return false;
        }

        if (response is not
            {
                Dt: { } dt,
                Main: { Temp: { } temp, TempMin: { } tempMin, TempMax: { } tempMax },
                Sys.Country: { Length: > 0 } country,
            })
        {
            error = "Response is missing one of the required fields: dt, main.temp, main.temp_min, main.temp_max, sys.country";
            return false;
        }

        weather = new CurrentWeather(
            country,
            temp,
            tempMin,
            tempMax,
            DateTimeOffset.FromUnixTimeSeconds(dt).UtcDateTime,
            body);
        error = null;
        return true;
    }

    private static string? ReadErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<OpenWeatherMapError>(body)?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

    // The API key is sent in the query string; make sure it never ends up in a stored error message.
    private static string Redact(string message, string apiKey) =>
        string.IsNullOrEmpty(apiKey) ? message : message.Replace(apiKey, Redacted, StringComparison.Ordinal);
}
