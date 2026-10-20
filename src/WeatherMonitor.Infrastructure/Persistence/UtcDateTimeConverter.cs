using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Stores every <see cref="DateTime"/> as UTC and restores <see cref="DateTimeKind.Utc"/> on read (datetime2 has no
/// kind). It also converts query parameters, so a value of unspecified kind is rejected, as in the domain: treating it
/// as local time would silently shift a filter by the server's UTC offset.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => ToUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException(
            "DateTime values stored in or compared with the database must be UTC (DateTimeKind.Utc).", nameof(value)),
    };
}
