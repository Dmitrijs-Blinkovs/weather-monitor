using System.Net;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class RoutingTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _factory = new();

    [Theory]
    [InlineData("/temperature")]
    [InlineData("/fetch-log")]
    public async Task Client_side_routes_are_served_the_single_page_app(string path)
    {
        using var response = await _factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(WeatherApiFactory.SpaIndexHtml);
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/logs/extra/segments")]
    public async Task Unknown_api_routes_are_not_found_instead_of_the_app(string path)
    {
        using var response = await _factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
