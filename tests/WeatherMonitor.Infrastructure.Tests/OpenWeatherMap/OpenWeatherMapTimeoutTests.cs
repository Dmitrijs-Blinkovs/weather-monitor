using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherMonitor.Application.Abstractions;

namespace WeatherMonitor.Infrastructure.Tests.OpenWeatherMap;

public sealed class OpenWeatherMapTimeoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_stalled_response_body_obeys_the_full_request_budget_and_preserves_caller_cancellation(bool cancelCaller)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var body = new StalledBody();
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = body })));
        services.AddOpenWeatherMap(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenWeatherMap:ApiKey"] = "test-key",
            // The same registration as production with a short budget; CompositionTests pins the production 30 s.
            ["OpenWeatherMap:RequestTimeout"] = "00:00:01",
        }).Build());
        await using var provider = services.BuildServiceProvider();
        var weatherProvider = provider.GetRequiredService<IWeatherProvider>();
        var request = weatherProvider.GetCurrentWeatherAsync("Riga", "LV", cancellation.Token);

        try
        {
            // Headers have already passed through the real resilience handler; only the body is stalled.
            await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancelCaller)
            {
                await cancellation.CancelAsync();
                await Should.ThrowAsync<OperationCanceledException>(() => request);
            }
            else
            {
                // The budget here is 1 s. The guard keeps a regression (no HttpClient.Timeout) from hanging the run.
                var result = await request.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
                result.IsSuccess.ShouldBeFalse();
                result.HttpStatusCode.ShouldBeNull();
                result.Error.ShouldBe("Request timed out");
                result.ResponseBody.ShouldBeNull();
            }
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    private sealed class StalledBody : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
