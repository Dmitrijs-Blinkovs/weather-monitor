using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Domain;
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
    public async Task Deleting_old_attempts_removes_their_snapshots_and_keeps_newer_data()
    {
        await SeedAsync(
            Success("London", Now.AddDays(-100)),
            Failure("Rome", Now.AddDays(-95)),
            Success("Riga", Now.AddDays(-91)),
            Success("London", Now.AddDays(-1)));

        await using (var context = CreateContext())
        {
            var deleted = await new FetchAttemptRepository(context)
                .DeleteOlderThanAsync(Now.AddDays(-90), batchSize: 2, TestContext.Current.CancellationToken);
            deleted.ShouldBe(2); // one batch, oldest first
        }

        await using var verify = CreateContext();
        (await verify.FetchAttempts.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        var remainingSnapshots = await verify.WeatherSnapshots.Select(snapshot => snapshot.City).ToListAsync(TestContext.Current.CancellationToken);
        remainingSnapshots.ShouldBe(["Riga", "London"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_purge_batch_takes_the_oldest_attempts_by_time_not_by_insertion_order()
    {
        await SeedAsync(Success("Riga", Now.AddDays(-91)));
        await SeedAsync(Success("London", Now.AddDays(-100)));
        await SeedAsync(Failure("Rome", Now.AddDays(-95)));

        await using (var context = CreateContext())
        {
            (await new FetchAttemptRepository(context)
                .DeleteOlderThanAsync(Now.AddDays(-90), batchSize: 2, TestContext.Current.CancellationToken))
                .ShouldBe(2);
        }

        await using var verify = CreateContext();
        (await verify.FetchAttempts.Select(attempt => attempt.City).SingleAsync(TestContext.Current.CancellationToken))
            .ShouldBe("Riga");
    }

    [Fact]
    public async Task A_failed_attempt_keeps_its_response_body_and_the_country_code_round_trips()
    {
        const string body = """{"cod":"404","message":"city not found"}""";
        await SeedAsync(
            FetchAttempt.Failed("Rome", Now, TimeSpan.FromMilliseconds(80), 404, "city not found", body),
            Success("Riga", Now));

        await using var context = CreateContext();
        (await context.FetchAttempts.SingleAsync(attempt => !attempt.IsSuccess, TestContext.Current.CancellationToken))
            .ResponseBody.ShouldBe(body);
        (await context.WeatherSnapshots.SingleAsync(TestContext.Current.CancellationToken)).Country.ShouldBe("GB");
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
