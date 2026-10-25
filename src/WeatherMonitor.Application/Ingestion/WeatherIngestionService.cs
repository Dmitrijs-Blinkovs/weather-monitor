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
    IngestionMetrics metrics,
    ILogger<WeatherIngestionService> logger) : IWeatherIngestionService
{
    internal const string CancelledMessage =
        "Cancelled before completion (the host was shutting down or the run timed out).";

    /// <summary>
    /// How long saving a run may take. It is independent of the host's cancellation, so a run whose results exist is
    /// stored even when the host starts shutting down meanwhile. Fetching (OpenWeatherMap:RequestTimeout, 30 s by default and
    /// at most 40 s) plus this stays below the Functions timeout of 55 s.
    /// </summary>
    private static readonly TimeSpan SaveBudget = TimeSpan.FromSeconds(10);

    public async Task<IngestionSummary> IngestAsync(CancellationToken cancellationToken)
    {
        // Cities are fetched independently and in parallel: one failing city never blocks the others.
        var attempts = await Task.WhenAll(
            options.Value.Cities.Select(city => FetchAsync(city.Name, city.CountryCode, cancellationToken)));

        var summary = new IngestionSummary(
            Succeeded: attempts.Count(attempt => attempt.IsSuccess),
            Failed: attempts.Count(attempt => !attempt.IsSuccess));

        // Fetching follows the host's token, saving does not: even a cancelled run is stored (finished cities keep their
        // result, unfinished ones are failures), also when the cancellation arrives during the save. There is no retry
        // at this layer; EF still retries transient SQL errors, so this is not an exactly-once guarantee.
        using (var saveBudget = new CancellationTokenSource(SaveBudget))
        {
            await SaveAsync(attempts, saveBudget.Token);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            LogIngestionCancelled(logger, summary.Succeeded, summary.Failed);
            cancellationToken.ThrowIfCancellationRequested();
        }

        LogIngestionCompleted(logger, summary.Succeeded, summary.Failed);
        return summary;
    }

    private async Task SaveAsync(FetchAttempt[] attempts, CancellationToken cancellationToken)
    {
        try
        {
            await repository.AddRangeAsync(attempts, cancellationToken);
        }
        catch (Exception exception)
        {
            // The whole run is lost (database down or full, a value the schema rejects, the save budget used up). The
            // runtime records the failed invocation too; this names what was lost. Rethrown so the invocation fails and
            // the next minute retries.
            LogSaveFailed(logger, exception, attempts.Length);
            throw;
        }

        // Counted only once stored: the "no weather data" alert sums successes, so it also fires when writes fail.
        foreach (var attempt in attempts)
        {
            metrics.Record(attempt);
        }
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
            return FetchAttempt.Failed(
                city, attemptedAtUtc, duration, result.HttpStatusCode, result.Error, result.ResponseBody);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FetchAttempt.Failed(
                city, attemptedAtUtc, timeProvider.GetElapsedTime(startedAt), httpStatusCode: null, CancelledMessage);
        }
        catch (Exception exception)
        {
            // The provider reports expected failures as results; this is the safety net for anything else.
            // The exception text is only logged, never stored, so request details cannot leak into the fetch log.
            LogFetchCrashed(logger, exception, city);
            return FetchAttempt.Failed(
                city,
                attemptedAtUtc,
                timeProvider.GetElapsedTime(startedAt),
                httpStatusCode: null,
                $"Unexpected error ({exception.GetType().Name}); see the function logs.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Weather ingestion completed: {Succeeded} succeeded, {Failed} failed")]
    private static partial void LogIngestionCompleted(ILogger logger, int succeeded, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Weather ingestion was cancelled; stored {Succeeded} succeeded and {Failed} failed or unfinished attempts")]
    private static partial void LogIngestionCancelled(ILogger logger, int succeeded, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetching weather for {City} failed with HTTP {StatusCode}: {Error}")]
    private static partial void LogFetchFailed(ILogger logger, string city, int? statusCode, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Storing {Count} fetch attempts failed; the run's data is lost")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fetching weather for {City} threw an unexpected exception")]
    private static partial void LogFetchCrashed(ILogger logger, Exception exception, string city);
}
