using System.Net;
using System.Net.Http.Headers;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Web.Tests.Api;

public sealed class HealthAndCachingTests : IAsyncDisposable
{
    private readonly WeatherApiFactory _factory = new();

    [Fact]
    public async Task Liveness_does_not_depend_on_the_database()
    {
        // The test host has no database configured at all.
        using var response = await _factory.CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Readiness_reports_unhealthy_when_the_database_is_unreachable()
    {
        // Port 1 on loopback refuses connections immediately.
        using var unreachable = _factory.WithWebHostBuilder(builder => builder.UseSetting(
            "ConnectionStrings:WeatherDb",
            "Server=tcp:127.0.0.1,1;Database=WeatherMonitor;Connect Timeout=2;Encrypt=False"));

        using var response = await unreachable.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Identical_requests_within_the_cache_window_are_answered_without_the_database()
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(["London", "Riga", "Rome"]);
        using var client = _factory.CreateClient();

        var first = await client.GetStringAsync("/api/cities", TestContext.Current.CancellationToken);
        var second = await client.GetStringAsync("/api/cities", TestContext.Current.CancellationToken);

        second.ShouldBe(first);
        await _factory.FetchLogReader.Received(1).GetCitiesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_client_abandoning_a_request_does_not_fail_an_identical_concurrent_request()
    {
        // React StrictMode and page navigation abort a request while an identical one is already on its way. With the
        // output cache's request coalescing, the second request would wait for the first and inherit its cancellation.
        var ct = TestContext.Current.CancellationToken;
        var firstCallStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstCallStarted.SetResult();
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            }

            return (IReadOnlyList<string>)["London", "Riga", "Rome"];
        });
        using var client = _factory.CreateClient();
        using var abandon = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var abandoned = client.GetAsync("/api/cities", abandon.Token);
        await firstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        var second = client.GetAsync("/api/cities", ct);
        // Let the second request reach the output cache while the first one is still running, then abandon the first.
        await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(500), ct));
        await abandon.CancelAsync();

        using var response = await second;
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await Should.ThrowAsync<TaskCanceledException>(abandoned);
    }

    [Fact]
    public async Task Different_queries_are_cached_separately()
    {
        _factory.FetchLogReader.GetPageAsync(Arg.Any<FetchLogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<FetchLogEntryDto>([], 1, 50, 0));
        using var client = _factory.CreateClient();

        await client.GetStringAsync("/api/logs?city=Riga", TestContext.Current.CancellationToken);
        await client.GetStringAsync("/api/logs?city=Rome", TestContext.Current.CancellationToken);

        await _factory.FetchLogReader.Received(2).GetPageAsync(Arg.Any<FetchLogQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Json_responses_are_compressed_when_the_client_accepts_it()
    {
        _factory.FetchLogReader.GetCitiesAsync(Arg.Any<CancellationToken>()).Returns(["London", "Riga", "Rome"]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cities");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        using var response = await _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.Content.Headers.ContentEncoding.ShouldContain("br");
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
