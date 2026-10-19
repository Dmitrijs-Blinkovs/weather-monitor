using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Ingestion;

internal sealed partial class WeatherIngestionService(
    IWeatherProvider weatherProvider,
    IFetchAttemptRepository repository,
    IOptions<WeatherIngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<WeatherIngestionService> logger) : IWeatherIngestionService
{
    public async Task<IngestionSummary> IngestAsync(CancellationToken cancellationToken)
    {
        // Cities are fetched independently and in parallel: one failing city never blocks the others.
        var attempts = await Task.WhenAll(
            options.Value.Cities.Select(city => FetchAsync(city.Name, city.CountryCode, cancellationToken)));

        await repository.AddRangeAsync(attempts, cancellationToken);

        var summary = new IngestionSummary(
            Succeeded: attempts.Count(attempt => attempt.IsSuccess),
            Failed: attempts.Count(attempt => !attempt.IsSuccess));

        LogIngestionCompleted(logger, summary.Succeeded, summary.Failed);
        return summary;
    }

    private async Task<FetchAttempt> FetchAsync(string city, string? countryCode, CancellationToken cancellationToken)
    {
        var attemptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var startedAt = timeProvider.GetTimestamp();

        try
        {
            var result = await weatherProvider.GetCurrentWeatherAsync(city, countryCode, cancellationToken);
            var duration = timeProvider.GetElapsedTime(startedAt);

            if (result.IsSuccess)
            {
                return FetchAttempt.Succeeded(city, attemptedAtUtc, duration, result.HttpStatusCode, result.Weather);
            }

            LogFetchFailed(logger, city, result.HttpStatusCode, result.Error);
            return FetchAttempt.Failed(city, attemptedAtUtc, duration, result.HttpStatusCode, result.Error);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The provider is expected to report failures as results; this is the safety net for anything else.
            LogFetchCrashed(logger, exception, city);
            return FetchAttempt.Failed(
                city, attemptedAtUtc, timeProvider.GetElapsedTime(startedAt), httpStatusCode: null, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Weather ingestion completed: {Succeeded} succeeded, {Failed} failed")]
    private static partial void LogIngestionCompleted(ILogger logger, int succeeded, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetching weather for {City} failed with HTTP {StatusCode}: {Error}")]
    private static partial void LogFetchFailed(ILogger logger, string city, int? statusCode, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fetching weather for {City} threw an unexpected exception")]
    private static partial void LogFetchCrashed(ILogger logger, Exception exception, string city);
}
