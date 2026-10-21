using System.Net;
using System.Text.Json;
using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class WeatherReadingsApiTests : IAsyncDisposable
{
    private const string Range = "from=2026-10-19T10:00:00Z&to=2026-10-19T12:00:00Z";

    private readonly WeatherApiFactory _factory = new();

    public WeatherReadingsApiTests()
    {
        _factory.ReadingsReader
            .GetPageAsync(Arg.Any<WeatherReadingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new PagedResult<WeatherReadingDto>(
                [], call.Arg<WeatherReadingsQuery>().Page, call.Arg<WeatherReadingsQuery>().PageSize, 0));
    }

    [Fact]
    public async Task Returns_a_page_of_readings_as_camel_case_json()
    {
        var fetchedAt = new DateTime(2026, 10, 19, 11, 59, 0, DateTimeKind.Utc);
        _factory.ReadingsReader
            .GetPageAsync(Arg.Any<WeatherReadingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WeatherReadingDto>(
                [new WeatherReadingDto(42, fetchedAt, "Rome", "IT", 18.4, 17, 19.9, fetchedAt.AddMinutes(-7))], 1, 50, 1));

        using var response = await _factory.CreateClient()
            .GetAsync($"/api/weather/readings?{Range}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var reading = json.RootElement.GetProperty("items")[0];
        reading.GetProperty("id").GetInt64().ShouldBe(42);
        reading.GetProperty("fetchedAtUtc").GetString().ShouldBe("2026-10-19T11:59:00Z");
        reading.GetProperty("city").GetString().ShouldBe("Rome");
        reading.GetProperty("country").GetString().ShouldBe("IT");
        reading.GetProperty("temperature").GetDouble().ShouldBe(18.4);
        reading.GetProperty("temperatureMin").GetDouble().ShouldBe(17);
        reading.GetProperty("temperatureMax").GetDouble().ShouldBe(19.9);
        reading.GetProperty("observedAtUtc").GetString().ShouldBe("2026-10-19T11:52:00Z");
        json.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Forwards_the_range_in_utc_and_the_paging_to_the_reader()
    {
        using var response = await _factory.CreateClient().GetAsync(
            "/api/weather/readings?from=2026-10-19T10:00:00%2B03:00&to=2026-10-19T12:00:00Z&page=2&pageSize=10",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.ReadingsReader.Received(1).GetPageAsync(
            Arg.Is<WeatherReadingsQuery>(query =>
                query.FromUtc == new DateTime(2026, 10, 19, 7, 0, 0) && query.FromUtc.Kind == DateTimeKind.Utc
                && query.ToUtc == new DateTime(2026, 10, 19, 12, 0, 0) && query.ToUtc.Kind == DateTimeKind.Utc
                && query.Page == 2 && query.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_the_first_page_and_default_size_when_no_paging_is_given()
    {
        using var response = await _factory.CreateClient()
            .GetAsync($"/api/weather/readings?{Range}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.ReadingsReader.Received(1).GetPageAsync(
            Arg.Is<WeatherReadingsQuery>(query => query.Page == 1 && query.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-19T10:00:00Z")]
    [InlineData("?from=2026-10-19T12:00:00Z&to=2026-10-19T10:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-10-19T00:00:00Z")]
    [InlineData("?" + Range + "&page=0")]
    [InlineData("?" + Range + "&page=1001")]
    [InlineData("?" + Range + "&pageSize=0")]
    [InlineData("?" + Range + "&pageSize=201")]
    public async Task Rejects_an_invalid_range_or_page_with_a_validation_problem(string query)
    {
        using var response = await _factory.CreateClient()
            .GetAsync($"/api/weather/readings{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        await _factory.ReadingsReader.DidNotReceive()
            .GetPageAsync(Arg.Any<WeatherReadingsQuery>(), Arg.Any<CancellationToken>());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
