using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Domain;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

/// <summary>
/// An in-memory SQLite database with the real EF Core model: fast, isolated per test class,
/// and it executes the LINQ queries as SQL (unlike the EF in-memory provider).
/// </summary>
public abstract class SqliteTestDatabase : IAsyncLifetime
{
    protected static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public virtual async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected WeatherDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WeatherDbContext>().UseSqlite(_connection).Options);

    protected async Task SeedAsync(params FetchAttempt[] attempts)
    {
        await using var context = CreateContext();
        context.FetchAttempts.AddRange(attempts);
        await context.SaveChangesAsync();
    }

    protected static FetchAttempt Success(string city, DateTime attemptedAtUtc, double temperature = 10, DateTime? observedAtUtc = null) =>
        FetchAttempt.Succeeded(
            city,
            attemptedAtUtc,
            TimeSpan.FromMilliseconds(120),
            200,
            new CurrentWeather("GB", temperature, temperature - 1, temperature + 1, observedAtUtc ?? attemptedAtUtc, "{}"));

    protected static FetchAttempt Failure(string city, DateTime attemptedAtUtc) =>
        FetchAttempt.Failed(city, attemptedAtUtc, TimeSpan.FromMilliseconds(80), 500, "Server error");
}
