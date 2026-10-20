using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence.Configurations;

internal sealed class FetchAttemptConfiguration : IEntityTypeConfiguration<FetchAttempt>
{
    public void Configure(EntityTypeBuilder<FetchAttempt> builder)
    {
        builder.ToTable("FetchAttempts");
        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.City).HasMaxLength(FetchAttempt.CityMaxLength).IsRequired();
        builder.Property(attempt => attempt.ErrorMessage).HasMaxLength(FetchAttempt.ErrorMessageMaxLength);

        builder.HasOne(attempt => attempt.Snapshot)
            .WithOne()
            .HasForeignKey<WeatherSnapshot>(snapshot => snapshot.FetchAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        // The log page lists attempts newest first, optionally filtered by city or by result; the retention purge and
        // the data-freshness health check also seek on the attempt time.
        builder.HasIndex(attempt => attempt.AttemptedAtUtc);
        builder.HasIndex(attempt => new { attempt.City, attempt.AttemptedAtUtc });
        builder.HasIndex(attempt => new { attempt.IsSuccess, attempt.AttemptedAtUtc });
    }
}
