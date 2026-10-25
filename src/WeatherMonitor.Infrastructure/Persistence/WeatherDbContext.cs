using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence;

public sealed class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<FetchAttempt> FetchAttempts => Set<FetchAttempt>();

    public DbSet<WeatherSnapshot> WeatherSnapshots => Set<WeatherSnapshot>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored as UTC and read back with DateTimeKind.Utc, so they serialize with a "Z" suffix.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeatherDbContext).Assembly);
    }
}
