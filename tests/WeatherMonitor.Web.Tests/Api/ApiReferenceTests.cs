using System.Net;
using System.Text.Json;
using WeatherMonitor.Web.Security;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class ApiReferenceTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _development = new("Development");
    private readonly WeatherApiFactory _production = new("Production");

    [Fact]
    public async Task Development_serves_the_interactive_api_reference_with_its_own_content_security_policy()
    {
        using var response = await _development.CreateClient()
            .GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Weather Monitor API");
        response.Headers.GetValues("Content-Security-Policy").Single()
            .ShouldBe(SecurityHeaders.ApiReferenceContentSecurityPolicy);
    }

    [Fact]
    public async Task Development_keeps_the_strict_content_security_policy_everywhere_else()
    {
        using var response = await _development.CreateClient()
            .GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.Headers.GetValues("Content-Security-Policy").Single().ShouldBe(SecurityHeaders.ContentSecurityPolicy);
    }

    [Fact]
    public async Task The_openapi_document_describes_the_real_endpoints_with_their_xml_comments()
    {
        var document = await _development.CreateClient()
            .GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(document);
        json.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Weather Monitor API");
        json.RootElement.GetProperty("paths").TryGetProperty("/api/{path}", out _).ShouldBeFalse();
        var readings = json.RootElement.GetProperty("paths").GetProperty("/api/weather/readings").GetProperty("get");
        readings.GetProperty("summary").GetString().ShouldStartWith("Returns a page of the weather readings");
        json.RootElement.GetProperty("paths").TryGetProperty("/api/logs/{id}", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Outside_development_neither_the_document_nor_the_reference_exists()
    {
        var client = _production.CreateClient();

        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var reference = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        document.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // An unknown path without a file extension is a client-side route: the SPA, under the strict policy.
        (await reference.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(WeatherApiFactory.SpaIndexHtml);
        reference.Headers.GetValues("Content-Security-Policy").Single().ShouldBe(SecurityHeaders.ContentSecurityPolicy);
    }

    public async ValueTask DisposeAsync()
    {
        await _development.DisposeAsync();
        await _production.DisposeAsync();
    }
}
