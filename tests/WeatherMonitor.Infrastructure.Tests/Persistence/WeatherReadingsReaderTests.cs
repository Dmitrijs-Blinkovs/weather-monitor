using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Paging;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class WeatherReadingsReaderTests : SqliteTestDatabase
{
    private static readonly DateTime From = Now.AddHours(-1);

    [Fact]
    public async Task Every_fetch_is_a_reading_even_when_the_provider_returned_the_same_observation()
    {
        var observedAt = Now.AddMinutes(-12);
        await SeedAsync(
            Success("London", Now.AddMinutes(-3), temperature: 11, observedAtUtc: observedAt),
            Success("London", Now.AddMinutes(-2), temperature: 11, observedAtUtc: observedAt),
            Success("London", Now.AddMinutes(-1), temperature: 11, observedAtUtc: observedAt));

        var page = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 1, pageSize: 50));

        page.TotalCount.ShouldBe(3);
        page.Items.Select(reading => reading.FetchedAtUtc)
            .ShouldBe([Now.AddMinutes(-1), Now.AddMinutes(-2), Now.AddMinutes(-3)]);
        page.Items.ShouldAllBe(reading => reading.ObservedAtUtc == observedAt && reading.Temperature == 11);
    }

    [Fact]
    public async Task A_reading_carries_the_city_country_temperatures_and_both_times()
    {
        await SeedAsync(Success("Riga", Now.AddMinutes(-1), temperature: 4.5, observedAtUtc: Now.AddMinutes(-9)));

        var page = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 1, pageSize: 50));

        var reading = page.Items.ShouldHaveSingleItem();
        reading.Id.ShouldBePositive();
        reading.City.ShouldBe("Riga");
        reading.Country.ShouldBe("GB");
        (reading.Temperature, reading.TemperatureMin, reading.TemperatureMax).ShouldBe((4.5, 3.5, 5.5));
        reading.FetchedAtUtc.ShouldBe(Now.AddMinutes(-1));
        reading.FetchedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        reading.ObservedAtUtc.ShouldBe(Now.AddMinutes(-9));
    }

    [Fact]
    public async Task Only_successful_fetches_inside_the_range_count_with_inclusive_bounds()
    {
        await SeedAsync(
            Success("Rome", From.AddMinutes(-1)),
            Success("Rome", From),
            Failure("Rome", Now.AddMinutes(-30)),
            Success("Rome", Now),
            Success("Rome", Now.AddMinutes(1)));

        var page = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 1, pageSize: 50));

        page.TotalCount.ShouldBe(2);
        page.Items.Select(reading => reading.FetchedAtUtc).ShouldBe([Now, From]);
    }

    [Fact]
    public async Task Later_pages_continue_where_the_previous_page_ended()
    {
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Success("Riga", Now.AddMinutes(-1)),
            Success("Rome", Now.AddMinutes(-1)),
            Success("London", Now.AddMinutes(-2)),
            Success("Riga", Now.AddMinutes(-2)));

        var first = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 1, pageSize: 2));
        var second = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 2, pageSize: 2));
        var third = await ReadAsync(new WeatherReadingsQuery(From, Now, page: 3, pageSize: 2));

        third.TotalPages.ShouldBe(3);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(reading => reading.Id).ToList();
        ids.ShouldBeUnique();
        ids.Count.ShouldBe(5);
        third.Items.ShouldHaveSingleItem().FetchedAtUtc.ShouldBe(Now.AddMinutes(-2));
    }

    private async Task<PagedResult<WeatherReadingDto>> ReadAsync(WeatherReadingsQuery query)
    {
        await using var context = CreateContext();
        return await new WeatherReadingsReader(context).GetPageAsync(query, TestContext.Current.CancellationToken);
    }
}
