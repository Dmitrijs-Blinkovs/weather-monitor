namespace WeatherMonitor.Application.History;

/// <summary>Validation rules for a history request, shared by the API and the service.</summary>
public static class WeatherHistoryRules
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(31);

    public static IEnumerable<string> Validate(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc >= toUtc)
        {
            yield return "'from' must be earlier than 'to'.";
        }
        else if (toUtc - fromUtc > MaxRange)
        {
            yield return $"The requested range must not exceed {MaxRange.TotalDays:0} days.";
        }
    }
}
