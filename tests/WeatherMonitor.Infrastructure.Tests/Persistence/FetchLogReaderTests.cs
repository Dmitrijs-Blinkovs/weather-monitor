using WeatherMonitor.Application.FetchLog;
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
        page.Items.ShouldAllBe(entry => !entry.IsSuccess && entry.ErrorMessage == "Server error" && entry.HttpStatusCode == 500);
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

    private async Task<PagedResult<FetchLogEntryDto>> ReadAsync(FetchLogQuery query)
    {
        await using var context = CreateContext();
        return await new FetchLogReader(context).GetPageAsync(query, TestContext.Current.CancellationToken);
    }
}
