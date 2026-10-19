using WeatherMonitor.Application.History;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class WeatherHistoryReaderTests : SqliteTestDatabase
{
    private static readonly DateTime From = Now.AddHours(-6);

    [Fact]
    public async Task Returns_only_observations_inside_the_range_with_inclusive_bounds()
    {
        await SeedAsync(
            Success("Riga", Now, temperature: 1, observedAtUtc: From.AddMinutes(-1)),
            Success("Riga", Now, temperature: 2, observedAtUtc: From),
            Success("Riga", Now, temperature: 3, observedAtUtc: Now),
            Success("Riga", Now, temperature: 4, observedAtUtc: Now.AddMinutes(1)));

        var observations = await ReadAsync();

        observations.Select(observation => observation.Temperature).ShouldBe([2, 3], ignoreOrder: true);
    }

    [Fact]
    public async Task Repeated_fetches_of_the_same_observation_count_once()
    {
        var observedAt = Now.AddMinutes(-30);
        await SeedAsync(
            Success("London", Now.AddMinutes(-3), temperature: 11, observedAtUtc: observedAt),
            Success("London", Now.AddMinutes(-2), temperature: 11, observedAtUtc: observedAt),
            Success("London", Now.AddMinutes(-1), temperature: 11, observedAtUtc: observedAt),
            Success("London", Now, temperature: 12, observedAtUtc: Now.AddMinutes(-5)));

        var observations = await ReadAsync();

        observations.Count.ShouldBe(2);
        observations.ShouldContain(observation => observation.ObservedAtUtc == observedAt && observation.Temperature == 11);
    }

    [Fact]
    public async Task Failed_attempts_have_no_observations()
    {
        await SeedAsync(Failure("Rome", Now.AddMinutes(-1)));

        (await ReadAsync()).ShouldBeEmpty();
    }

    private async Task<IReadOnlyList<WeatherObservation>> ReadAsync()
    {
        await using var context = CreateContext();
        return await new WeatherHistoryReader(context).GetObservationsAsync(From, Now, TestContext.Current.CancellationToken);
    }
}
