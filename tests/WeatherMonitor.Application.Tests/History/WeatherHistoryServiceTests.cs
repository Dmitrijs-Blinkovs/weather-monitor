using WeatherMonitor.Application.History;

namespace WeatherMonitor.Application.Tests.History;

public sealed class WeatherHistoryServiceTests
{
    private static readonly DateTime From = TestData.Now.AddHours(-24);
    private static readonly DateTime To = TestData.Now;

    private readonly IWeatherHistoryReader _reader = Substitute.For<IWeatherHistoryReader>();

    [Fact]
    public async Task Groups_observations_into_one_series_per_city_ordered_by_name_and_time()
    {
        Returns(
            TestData.Observation("Rome", To.AddHours(-1), 18),
            TestData.Observation("London", To.AddHours(-1), 11),
            TestData.Observation("Rome", To.AddHours(-3), 16),
            TestData.Observation("London", To.AddHours(-2), 10));

        var history = await CreateService().GetHistoryAsync(From, To, TestContext.Current.CancellationToken);

        history.FromUtc.ShouldBe(From);
        history.ToUtc.ShouldBe(To);
        history.Cities.Select(series => series.City).ShouldBe(["London", "Rome"]);
        history.Cities[1].Points.Select(point => point.Temperature).ShouldBe([16, 18]);
    }

    [Fact]
    public async Task Summarises_each_city_with_its_latest_reading_and_the_extremes_of_the_period()
    {
        Returns(
            TestData.Observation("Riga", To.AddHours(-3), 5, min: 2, max: 6, country: "LV"),
            TestData.Observation("Riga", To.AddMinutes(-10), 7, min: 6, max: 9, country: "LV"),
            TestData.Observation("Riga", To.AddHours(-2), 4, min: 3, max: 5, country: "LV"));

        var riga = (await CreateService().GetHistoryAsync(From, To, TestContext.Current.CancellationToken)).Cities.Single();

        riga.Country.ShouldBe("LV");
        riga.LatestTemperature.ShouldBe(7);
        riga.LastUpdatedUtc.ShouldBe(To.AddMinutes(-10));
        riga.MinTemperature.ShouldBe(2);
        riga.MaxTemperature.ShouldBe(9);
    }

    [Fact]
    public async Task Keeps_one_point_per_observation_time_when_the_provider_revised_a_value()
    {
        var observedAt = To.AddHours(-1);
        Returns(
            TestData.Observation("Riga", observedAt, 5),
            TestData.Observation("Riga", observedAt, 5.4),
            TestData.Observation("Riga", To.AddMinutes(-10), 6));

        var riga = (await CreateService().GetHistoryAsync(From, To, TestContext.Current.CancellationToken)).Cities.Single();

        riga.Points.Select(point => point.ObservedAtUtc).ShouldBe([observedAt, To.AddMinutes(-10)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_revised_observation_shows_the_version_stored_last_whatever_the_read_order(bool newestFirst)
    {
        var observedAt = To.AddMinutes(-10);
        var first = TestData.Observation("Riga", observedAt, 10, latestSnapshotId: 7);
        var revised = TestData.Observation("Riga", observedAt, 20, latestSnapshotId: 9);
        Returns(newestFirst ? new[] { revised, first } : new[] { first, revised });

        var riga = (await CreateService().GetHistoryAsync(From, To, TestContext.Current.CancellationToken)).Cities.Single();

        riga.LatestTemperature.ShouldBe(20);
        riga.Points.ShouldHaveSingleItem().Temperature.ShouldBe(20);
        riga.MaxTemperature.ShouldBe(21);
    }

    [Fact]
    public async Task Returns_no_series_when_the_period_has_no_data()
    {
        Returns();

        var history = await CreateService().GetHistoryAsync(From, To, TestContext.Current.CancellationToken);

        history.Cities.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejects_a_range_whose_start_is_not_before_its_end()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            CreateService().GetHistoryAsync(To, From, TestContext.Current.CancellationToken));

        await _reader.DidNotReceive()
            .GetObservationsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_the_maximum()
    {
        var tooEarly = To - WeatherHistoryRules.MaxRange - TimeSpan.FromMinutes(1);

        await Should.ThrowAsync<ArgumentException>(() =>
            CreateService().GetHistoryAsync(tooEarly, To, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_maximum_range_itself_is_allowed()
    {
        WeatherHistoryRules.Validate(To - WeatherHistoryRules.MaxRange, To).ShouldBeEmpty();
    }

    private void Returns(params WeatherObservation[] observations) =>
        _reader.GetObservationsAsync(From, To, Arg.Any<CancellationToken>()).Returns(observations);

    private WeatherHistoryService CreateService() => new(_reader);
}
