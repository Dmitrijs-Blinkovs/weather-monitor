using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherMonitor.Application.Ingestion;

namespace WeatherMonitor.Functions.Tests;

public sealed class FetchWeatherFunctionTests
{
    private readonly IWeatherIngestionService _ingestion = Substitute.For<IWeatherIngestionService>();

    [Fact]
    public void Runs_every_minute()
    {
        FetchWeatherFunction.EveryMinute.ShouldBe("0 */1 * * * *");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Each_run_delegates_to_the_ingestion_service_even_when_late(bool isPastDue)
    {
        var function = new FetchWeatherFunction(_ingestion, NullLogger<FetchWeatherFunction>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        await function.RunAsync(new TimerInfo { IsPastDue = isPastDue }, cancellationToken);

        await _ingestion.Received(1).IngestAsync(cancellationToken);
    }

    [Fact]
    public async Task A_failing_run_is_reported_to_the_functions_runtime()
    {
        _ingestion.IngestAsync(Arg.Any<CancellationToken>())
            .Returns<IngestionSummary>(_ => throw new InvalidOperationException("database unavailable"));
        var function = new FetchWeatherFunction(_ingestion, NullLogger<FetchWeatherFunction>.Instance);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            function.RunAsync(new TimerInfo(), TestContext.Current.CancellationToken));
    }
}
