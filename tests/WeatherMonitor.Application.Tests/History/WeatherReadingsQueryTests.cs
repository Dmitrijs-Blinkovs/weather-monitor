using WeatherMonitor.Application.History;

namespace WeatherMonitor.Application.Tests.History;

public sealed class WeatherReadingsQueryTests
{
    private static readonly DateTime From = TestData.Now.AddHours(-1);

    [Fact]
    public void A_range_that_breaks_the_history_rules_is_rejected_even_without_the_api_validation()
    {
        Should.Throw<ArgumentException>(() => new WeatherReadingsQuery(TestData.Now, From, 1, 50));
        Should.Throw<ArgumentException>(() => new WeatherReadingsQuery(TestData.Now.AddDays(-32), TestData.Now, 1, 50));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1001, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public void A_page_outside_the_limits_is_rejected(int page, int pageSize)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new WeatherReadingsQuery(From, TestData.Now, page, pageSize));
    }

    [Fact]
    public void The_offset_follows_from_the_page_and_its_size()
    {
        new WeatherReadingsQuery(From, TestData.Now, 3, 20).Skip.ShouldBe(40);
    }
}
