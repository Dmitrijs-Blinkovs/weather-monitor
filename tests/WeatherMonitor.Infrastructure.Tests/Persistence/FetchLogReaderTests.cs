using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.Paging;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class FetchLogReaderTests : SqliteTestDatabase
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Failure("Rome", Now.AddMinutes(-2)),
            Success("Riga", Now.AddMinutes(-3)),
            Failure("London", Now.AddMinutes(-4)),
            Success("Rome", Now.AddMinutes(-5)));
    }

    [Fact]
    public async Task Returns_the_newest_attempts_first_with_paging_information()
    {
        var page = await ReadAsync(new FetchLogQuery(page: 1, pageSize: 2));

        page.Items.Select(entry => (entry.City, entry.AttemptedAtUtc))
            .ShouldBe([("London", Now.AddMinutes(-1)), ("Rome", Now.AddMinutes(-2))]);
        page.TotalCount.ShouldBe(5);
        page.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task Later_pages_continue_where_the_previous_page_ended()
    {
        var page = await ReadAsync(new FetchLogQuery(page: 3, pageSize: 2));

        page.Items.Single().City.ShouldBe("Rome");
        page.Items.Single().AttemptedAtUtc.ShouldBe(Now.AddMinutes(-5));
    }

    [Fact]
    public async Task Filters_by_city()
    {
        var page = await ReadAsync(new FetchLogQuery(1, 50, city: "London"));

        page.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(entry => entry.City == "London");
    }

    [Fact]
    public async Task Filters_by_status()
    {
        var page = await ReadAsync(new FetchLogQuery(1, 50, status: FetchStatus.Failure));

        page.Items.Select(entry => entry.City).ShouldBe(["Rome", "London"]);
        page.Items.ShouldAllBe(entry => !entry.IsSuccess && entry.HttpStatusCode == 500);
    }

    [Fact]
    public async Task Paging_applies_to_the_filtered_attempts()
    {
        var page = await ReadAsync(new FetchLogQuery(page: 2, pageSize: 1, city: "London"));

        page.TotalCount.ShouldBe(2);
        var entry = page.Items.ShouldHaveSingleItem();
        entry.AttemptedAtUtc.ShouldBe(Now.AddMinutes(-4));
        entry.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Lists_every_city_once_in_alphabetical_order()
    {
        await using var context = CreateContext();

        var cities = await new FetchLogReader(context).GetCitiesAsync(TestContext.Current.CancellationToken);

        cities.ShouldBe(["London", "Riga", "Rome"]);
    }

    [Fact]
    public async Task The_details_of_a_success_carry_the_full_payload_from_its_snapshot()
    {
        var attempt = Success("Riga", Now.AddMinutes(1));
        await SeedAsync(attempt);

        var details = await ReadDetailsAsync(attempt.Id);

        details.ShouldNotBeNull();
        (details.City, details.IsSuccess, details.HttpStatusCode, details.ErrorMessage).ShouldBe(("Riga", true, 200, null));
        details.AttemptedAtUtc.ShouldBe(Now.AddMinutes(1));
        details.AttemptedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        details.ResponseBody.ShouldBe("{}");
    }

    [Fact]
    public async Task The_details_of_a_failure_carry_the_full_error_and_the_body_received()
    {
        var error = new string('x', 300);
        var attempt = Domain.FetchAttempt.Failed(
            "Rome", Now.AddMinutes(1), TimeSpan.FromMilliseconds(80), 401, error, """{"cod":401}""");
        var timeout = Domain.FetchAttempt.Failed("Rome", Now.AddMinutes(2), TimeSpan.FromSeconds(10), null, "Timed out");
        await SeedAsync(attempt, timeout);

        var details = await ReadDetailsAsync(attempt.Id);
        var timeoutDetails = await ReadDetailsAsync(timeout.Id);

        details.ShouldNotBeNull();
        (details.IsSuccess, details.HttpStatusCode, details.ErrorMessage).ShouldBe((false, 401, error));
        details.ResponseBody.ShouldBe("""{"cod":401}""");
        timeoutDetails.ShouldNotBeNull();
        timeoutDetails.ResponseBody.ShouldBeNull();
    }

    [Fact]
    public async Task An_unknown_attempt_has_no_details()
    {
        (await ReadDetailsAsync(long.MaxValue)).ShouldBeNull();
    }

    private async Task<FetchAttemptDetailsDto?> ReadDetailsAsync(long id)
    {
        await using var context = CreateContext();
        return await new FetchLogReader(context).GetDetailsAsync(id, TestContext.Current.CancellationToken);
    }

    private async Task<PagedResult<FetchLogEntryDto>> ReadAsync(FetchLogQuery query)
    {
        await using var context = CreateContext();
        return await new FetchLogReader(context).GetPageAsync(query, TestContext.Current.CancellationToken);
    }
}
