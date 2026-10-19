using System.Net;
using System.Text.Json;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class WeatherHistoryApiTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _factory = new();

    public WeatherHistoryApiTests()
    {
        _factory.HistoryService
            .GetHistoryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call => new WeatherHistoryDto(call.ArgAt<DateTime>(0), call.ArgAt<DateTime>(1), []));
    }

    [Fact]
    public async Task Converts_the_requested_range_to_utc()
    {
        using var response = await _factory.CreateClient().GetAsync(
            "/api/weather/history?from=2026-10-19T10:00:00%2B03:00&to=2026-10-19T12:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.HistoryService.Received(1).GetHistoryAsync(
            Arg.Is<DateTime>(from => from == new DateTime(2026, 10, 19, 7, 0, 0) && from.Kind == DateTimeKind.Utc),
            Arg.Is<DateTime>(to => to == new DateTime(2026, 10, 19, 12, 0, 0) && to.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_each_city_with_its_series_and_summary()
    {
        var observedAt = new DateTime(2026, 10, 19, 11, 50, 0, DateTimeKind.Utc);
        _factory.HistoryService
            .GetHistoryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new WeatherHistoryDto(
                observedAt.AddHours(-1),
                observedAt,
                [new CityTemperatureSeriesDto("Rome", "IT", 18.4, 15.2, 19.9, observedAt, [new TemperaturePointDto(observedAt, 18.4, 17, 19.9)])]));

        var body = await _factory.CreateClient().GetStringAsync(
            "/api/weather/history?from=2026-10-19T10:50:00Z&to=2026-10-19T11:50:00Z",
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(body);
        var rome = json.RootElement.GetProperty("cities")[0];
        rome.GetProperty("city").GetString().ShouldBe("Rome");
        rome.GetProperty("country").GetString().ShouldBe("IT");
        rome.GetProperty("latestTemperature").GetDouble().ShouldBe(18.4);
        rome.GetProperty("minTemperature").GetDouble().ShouldBe(15.2);
        rome.GetProperty("maxTemperature").GetDouble().ShouldBe(19.9);
        rome.GetProperty("lastUpdatedUtc").GetString().ShouldBe("2026-10-19T11:50:00Z");
        rome.GetProperty("points").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-19T10:00:00Z")]
    [InlineData("?from=not-a-date&to=2026-10-19T10:00:00Z")]
    [InlineData("?from=2026-10-19T12:00:00Z&to=2026-10-19T10:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-10-19T00:00:00Z")]
    public async Task Rejects_a_missing_invalid_or_too_long_range(string query)
    {
        using var response = await _factory.CreateClient()
            .GetAsync($"/api/weather/history{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        await _factory.HistoryService.DidNotReceive()
            .GetHistoryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
