using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class MigrationTests
{
    [Fact]
    public void The_migrations_are_in_sync_with_the_model()
    {
        // The other persistence tests build the schema from the model (EnsureCreated on SQLite), so a forgotten
        // migration would go unnoticed there. This compares the model with the last migration snapshot;
        // no database connection is opened.
        var options = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var context = new WeatherDbContext(options);

        context.Database.HasPendingModelChanges().ShouldBeFalse(
            "the model changed without a migration - run `dotnet ef migrations add` (see docs/runbooks/add-ef-migration.md)");
    }
}
