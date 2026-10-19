using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence.Configurations;

internal sealed class WeatherSnapshotConfiguration : IEntityTypeConfiguration<WeatherSnapshot>
{
    public void Configure(EntityTypeBuilder<WeatherSnapshot> builder)
    {
        builder.ToTable("WeatherSnapshots");
        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.City).HasMaxLength(FetchAttempt.CityMaxLength).IsRequired();
        builder.Property(snapshot => snapshot.Country).HasMaxLength(WeatherSnapshot.CountryMaxLength).IsRequired();
        builder.Property(snapshot => snapshot.RawPayload).IsRequired();

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
