using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Application.Retention;

namespace WeatherMonitor.Application.Tests.Retention;

public sealed class DataRetentionServiceTests
{
    private readonly IFetchAttemptRepository _repository = Substitute.For<IFetchAttemptRepository>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(TestData.Now));

    [Fact]
    public async Task Deletes_everything_older_than_the_retention_period_in_batches()
    {
        _repository.DeleteOlderThanAsync(Arg.Any<DateTime>(), 100, Arg.Any<CancellationToken>())
            .Returns(100, 100, 37);

        var deleted = await CreateService(days: 90, batchSize: 100).PurgeAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(237);
        await _repository.Received(3).DeleteOlderThanAsync(TestData.Now.AddDays(-90), 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stops_after_one_call_when_there_is_nothing_to_delete()
    {
        _repository.DeleteOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        var deleted = await CreateService(days: 30).PurgeAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
        await _repository.Received(1).DeleteOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Zero_days_keeps_everything()
    {
        var deleted = await CreateService(days: 0).PurgeAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
        await _repository.DidNotReceive()
            .DeleteOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private DataRetentionService CreateService(int days, int batchSize = 5_000) =>
        new(
            _repository,
            Options.Create(new DataRetentionOptions { Days = days, BatchSize = batchSize }),
            _timeProvider,
            NullLogger<DataRetentionService>.Instance);
}
