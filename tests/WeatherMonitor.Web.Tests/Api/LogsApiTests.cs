using System.Net;
using System.Text.Json;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class LogsApiTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _factory = new();

    public LogsApiTests()
    {
        _factory.FetchLogReader
            .GetPageAsync(Arg.Any<FetchLogQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new PagedResult<FetchLogEntryDto>([], call.Arg<FetchLogQuery>().Page, call.Arg<FetchLogQuery>().PageSize, 0));
    }

    [Fact]
    public async Task Returns_a_page_of_attempts_as_camel_case_json()
    {
        var attemptedAt = new DateTime(2026, 10, 19, 9, 30, 0, DateTimeKind.Utc);
        _factory.FetchLogReader
            .GetPageAsync(Arg.Any<FetchLogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FetchLogEntryDto>(
                [new FetchLogEntryDto(7, "Riga", attemptedAt, false, 503, 812)], 1, 50, 1));

        using var response = await _factory.CreateClient().GetAsync("/api/logs", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var entry = json.RootElement.GetProperty("items")[0];
        entry.GetProperty("city").GetString().ShouldBe("Riga");
        entry.GetProperty("attemptedAtUtc").GetString().ShouldBe("2026-10-19T09:30:00Z");
        entry.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        entry.GetProperty("httpStatusCode").GetInt32().ShouldBe(503);
        entry.TryGetProperty("errorMessage", out _).ShouldBeFalse(); // loaded per attempt, never with the list
        json.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Uses_the_first_page_and_default_size_when_no_query_is_given()
    {
        using var response = await _factory.CreateClient().GetAsync("/api/logs", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.FetchLogReader.Received(1)
            .GetPageAsync(new FetchLogQuery(1, 50, null, null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Forwards_paging_and_filters_to_the_reader()
    {
        using var response = await _factory.CreateClient()
            .GetAsync("/api/logs?page=2&pageSize=10&city=Riga&status=Failure", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.FetchLogReader.Received(1)
            .GetPageAsync(new FetchLogQuery(2, 10, "Riga", FetchStatus.Failure), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("pageSize=500")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    [InlineData("page=2147483647")]
    [InlineData("page=1001")]
    [InlineData("status=Maybe")]
    public async Task Rejects_invalid_queries_with_a_validation_problem(string query)
    {
        using var response = await _factory.CreateClient().GetAsync($"/api/logs?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        await _factory.FetchLogReader.DidNotReceive()
            .GetPageAsync(Arg.Any<FetchLogQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_one_attempt_with_its_response_body()
    {
        var attemptedAt = new DateTime(2026, 10, 19, 9, 30, 0, DateTimeKind.Utc);
        _factory.FetchLogReader.GetDetailsAsync(7, Arg.Any<CancellationToken>())
            .Returns(new FetchAttemptDetailsDto(7, "Rome", attemptedAt, false, 401, "Invalid API key", 95, """{"cod":401}"""));

        using var response = await _factory.CreateClient().GetAsync("/api/logs/7", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("id").GetInt64().ShouldBe(7);
        json.RootElement.GetProperty("attemptedAtUtc").GetString().ShouldBe("2026-10-19T09:30:00Z");
        json.RootElement.GetProperty("errorMessage").GetString().ShouldBe("Invalid API key");
        json.RootElement.GetProperty("responseBody").GetString().ShouldBe("""{"cod":401}""");
    }

    [Fact]
    public async Task An_unknown_attempt_is_a_404_problem()
    {
        using var response = await _factory.CreateClient().GetAsync("/api/logs/42", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    public async Task An_attempt_id_that_is_not_a_positive_number_is_not_found_without_asking_the_reader(string id)
    {
        using var response = await _factory.CreateClient().GetAsync($"/api/logs/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await _factory.FetchLogReader.DidNotReceive().GetDetailsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lists_the_known_cities()
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(["London", "Riga", "Rome"]);

        var cities = await _factory.CreateClient()
            .GetStringAsync("/api/cities", TestContext.Current.CancellationToken);

        cities.ShouldBe("""["London","Riga","Rome"]""");
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
