using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class SecurityTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _factory = new();

    [Theory]
    [InlineData("/api/cities")]
    [InlineData("/temperature")]
    public async Task Every_response_carries_the_browser_hardening_headers(string path)
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(["Riga"]);

        using var response = await _factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var headers = response.Headers;
        headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'self'");
        headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
        headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
        headers.GetValues("Referrer-Policy").Single().ShouldBe("strict-origin-when-cross-origin");
        headers.Contains("Permissions-Policy").ShouldBeTrue();
    }

    [Fact]
    public async Task Error_responses_keep_the_hardening_headers()
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => throw new InvalidOperationException("database down"));

        using var response = await _factory.CreateClient().GetAsync("/api/cities", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'self'");
        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("database down");
    }

    [Fact]
    public async Task Too_many_api_requests_from_one_client_are_rejected_with_429()
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(["Riga"]);
        using var limited = _factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:PermitsPerMinute", "2"));
        using var client = limited.CreateClient();

        (await client.GetAsync("/api/cities", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/logs", TestContext.Current.CancellationToken)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        using var rejected = await client.GetAsync("/api/cities", TestContext.Current.CancellationToken);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        rejected.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_liveness_probe_is_never_rate_limited()
    {
        using var limited = _factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:PermitsPerMinute", "1"));
        using var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
