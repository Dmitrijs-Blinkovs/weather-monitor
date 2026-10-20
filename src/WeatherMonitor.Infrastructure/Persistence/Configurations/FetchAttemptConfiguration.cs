using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures only what differs from the EF Core conventions. By convention: the table is named after the DbSet, <c>Id</c>
/// is the key, non-nullable strings are NOT NULL (nullable reference types), and the snapshot is a one-to-one dependent
/// through <c>WeatherSnapshot.FetchAttemptId</c> (unique, cascade delete). MigrationTests fails if any of that changes.
/// </summary>
internal sealed class FetchAttemptConfiguration : IEntityTypeConfiguration<FetchAttempt>
{
    public void Configure(EntityTypeBuilder<FetchAttempt> builder)
    {
        // The domain's limits; without a length City would be nvarchar(max), which cannot be an index key.
        builder.Property(attempt => attempt.City).HasMaxLength(FetchAttempt.CityMaxLength);
        builder.Property(attempt => attempt.ErrorMessage).HasMaxLength(FetchAttempt.ErrorMessageMaxLength);

        // The log page lists attempts newest first, optionally filtered by city or by result; the retention purge and
        // the data-freshness health check also seek on the attempt time.
        builder.HasIndex(attempt => attempt.AttemptedAtUtc);
        builder.HasIndex(attempt => new { attempt.City, attempt.AttemptedAtUtc });
        builder.HasIndex(attempt => new { attempt.IsSuccess, attempt.AttemptedAtUtc });
    }
}
