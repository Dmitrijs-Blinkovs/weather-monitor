using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class UtcDateTimeConverterTests : SqliteTestDatabase
{
    private static readonly UtcDateTimeConverter Converter = new();

    [Fact]
    public async Task A_query_with_a_time_of_unspecified_kind_fails_instead_of_being_shifted_by_the_local_offset()
    {
        await SeedAsync(Success("Riga", Now));
        var unspecified = DateTime.SpecifyKind(Now.AddMinutes(1), DateTimeKind.Unspecified);
        await using var context = CreateContext();

        await Should.ThrowAsync<ArgumentException>(() => context.FetchAttempts
            .Where(attempt => attempt.AttemptedAtUtc < unspecified)
            .CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Utc_values_are_stored_unchanged()
    {
        var stored = (DateTime)Converter.ConvertToProvider(Now)!;

        stored.ShouldBe(Now);
        stored.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Local_values_are_stored_as_utc()
    {
        var local = Now.ToLocalTime();

        var stored = (DateTime)Converter.ConvertToProvider(local)!;

        stored.ShouldBe(Now);
        stored.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
