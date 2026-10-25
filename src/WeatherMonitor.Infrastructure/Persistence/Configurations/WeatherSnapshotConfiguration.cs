using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures only what differs from the EF Core conventions (see <see cref="FetchAttemptConfiguration"/>);
/// <c>RawPayload</c> stays nvarchar(max) NOT NULL by convention.
/// </summary>
internal sealed class WeatherSnapshotConfiguration : IEntityTypeConfiguration<WeatherSnapshot>
{
    public void Configure(EntityTypeBuilder<WeatherSnapshot> builder)
    {
        // The domain's limits; City is an index key, which nvarchar(max) cannot be. Country is an ISO code: char(2).
        builder.Property(snapshot => snapshot.City).HasMaxLength(FetchAttempt.CityMaxLength);
        builder.Property(snapshot => snapshot.Country)
            .HasMaxLength(CurrentWeather.CountryCodeLength).IsFixedLength().IsUnicode(false);

        // Covers the chart query (range on observation time) without touching the payload column.
        builder.HasIndex(snapshot => new { snapshot.ObservedAtUtc, snapshot.City })
            .IncludeProperties(snapshot => new
            {
                snapshot.Country,
                snapshot.Temperature,
                snapshot.TemperatureMin,
                snapshot.TemperatureMax,
            });
    }
}
