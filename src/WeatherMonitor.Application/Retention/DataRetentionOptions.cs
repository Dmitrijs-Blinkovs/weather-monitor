using System.ComponentModel.DataAnnotations;

namespace WeatherMonitor.Application.Retention;

/// <summary>How long fetch attempts and snapshots are kept. Bound from the "Retention" configuration section.</summary>
public sealed class DataRetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Days of history to keep; 0 keeps everything (no purge).</summary>
    [Range(0, 3650)]
    public int Days { get; init; } = 90;

    /// <summary>Rows deleted per statement, so a purge never holds long locks or a huge transaction log.</summary>
    [Range(100, 100_000)]
    public int BatchSize { get; init; } = 5_000;
}
