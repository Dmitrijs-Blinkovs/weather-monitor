using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Web.Models;

/// <summary>
/// A time range in the query string. Timestamps are ISO 8601 with an explicit offset, e.g. "2026-10-19T08:00:00Z";
/// a value without one is rejected rather than read in the server's time zone.
/// </summary>
public abstract class TimeRangeRequest : IValidatableObject
{
    [Required]
    [ModelBinder(typeof(ExplicitOffsetDateTimeBinder))]
    public DateTimeOffset? From { get; init; }

    [Required]
    [ModelBinder(typeof(ExplicitOffsetDateTimeBinder))]
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
