using System.Diagnostics.Metrics;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Ingestion;

/// <summary>
/// Metrics of the ingestion run (exported with OpenTelemetry): how many fetch attempts succeeded or failed per city,
/// and how long they took. Alerting on "no success for a city" works on these numbers, not on log text.
/// </summary>
public sealed class IngestionMetrics
{
    public const string MeterName = "WeatherMonitor.Ingestion";

    private readonly Counter<long> _attempts;
    private readonly Histogram<double> _duration;

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _attempts = meter.CreateCounter<long>(
            "weather.fetch.attempts", unit: "{attempt}", description: "Stored weather fetch attempts by city and outcome.");
        _duration = meter.CreateHistogram<double>(
            "weather.fetch.duration", unit: "ms", description: "Duration of a weather fetch attempt.");
    }

    public void Record(FetchAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var city = new KeyValuePair<string, object?>("city", attempt.City);
        var outcome = new KeyValuePair<string, object?>("outcome", attempt.IsSuccess ? "success" : "failure");
        _attempts.Add(1, city, outcome);
        _duration.Record(attempt.DurationMs, city, outcome);
    }
}
