using WeatherMonitor.Application.FetchLog;

namespace WeatherMonitor.Application.Tests.FetchLog;

public sealed class FetchLogQueryTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(1001, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public void A_page_outside_the_limits_is_rejected_even_without_the_api_validation(int page, int pageSize)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FetchLogQuery(page, pageSize));
    }

    [Fact]
    public void The_offset_follows_from_the_page_and_its_size()
    {
        new FetchLogQuery(3, 20).Skip.ShouldBe(40);
    }
}
