using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class FetchAttemptRepositoryTests : SqliteTestDatabase
{
    [Fact]
    public async Task Stores_attempts_together_with_the_snapshots_of_successful_ones()
    {
        await using (var context = CreateContext())
        {
            await new FetchAttemptRepository(context).AddRangeAsync(
                [Success("London", Now, temperature: 12.5), Failure("Rome", Now)],
                TestContext.Current.CancellationToken);
        }

        await using var verify = CreateContext();
        var attempts = await verify.FetchAttempts.Include(attempt => attempt.Snapshot)
            .OrderBy(attempt => attempt.City)
            .ToListAsync(TestContext.Current.CancellationToken);

        attempts.Count.ShouldBe(2);

        var london = attempts[0];
        london.IsSuccess.ShouldBeTrue();
        var snapshot = london.Snapshot.ShouldNotBeNull();
        snapshot.FetchAttemptId.ShouldBe(london.Id);
        snapshot.Temperature.ShouldBe(12.5);
        snapshot.RawPayload.ShouldBe("{}");

        var rome = attempts[1];
        rome.IsSuccess.ShouldBeFalse();
        rome.Snapshot.ShouldBeNull();
        rome.ErrorMessage.ShouldBe("Server error");
    }

    [Fact]
    public async Task Timestamps_are_read_back_as_utc()
    {
        await SeedAsync(Success("Riga", Now));

        await using var context = CreateContext();
        var snapshot = await context.WeatherSnapshots.SingleAsync(TestContext.Current.CancellationToken);

        snapshot.FetchedAtUtc.ShouldBe(Now);
        snapshot.FetchedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        snapshot.ObservedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
