using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Web.Models;

/// <summary>Query string of <c>GET /api/weather/history</c>. Timestamps are ISO 8601 with an offset, e.g. "2026-10-19T08:00:00Z".</summary>
public sealed class WeatherHistoryRequest : IValidatableObject
{
    [Required]
    public DateTimeOffset? From { get; init; }

    [Required]
    public DateTimeOffset? To { get; init; }

    internal DateTime FromUtc => From.GetValueOrDefault().UtcDateTime;

    internal DateTime ToUtc => To.GetValueOrDefault().UtcDateTime;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is null || To is null)
        {
            yield break;
        }

        foreach (var error in WeatherHistoryRules.Validate(FromUtc, ToUtc))
        {
            yield return new ValidationResult(error, [nameof(From), nameof(To)]);
        }
    }
}
