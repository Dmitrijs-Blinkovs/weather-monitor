using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WeatherMonitor.Application.Ingestion;

namespace WeatherMonitor.Functions;

/// <summary>Timer-triggered entry point; all the work is delegated to <see cref="IWeatherIngestionService"/>.</summary>
public sealed partial class FetchWeatherFunction(
    IWeatherIngestionService ingestionService,
    ILogger<FetchWeatherFunction> logger)
{
    /// <summary>NCRONTAB (with seconds): at second 0 of every minute.</summary>
    public const string EveryMinute = "0 */1 * * * *";

    [Function("FetchWeather")]
    public async Task RunAsync([TimerTrigger(EveryMinute)] TimerInfo timer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);

        if (timer.IsPastDue)
        {
            LogPastDue(logger);
        }

        await ingestionService.IngestAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timer is running late (past due)")]
    private static partial void LogPastDue(ILogger logger);
}
