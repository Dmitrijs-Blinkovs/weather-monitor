using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherMonitor.Application.Ingestion;

namespace WeatherMonitor.Functions.Tests;

public sealed class FetchWeatherFunctionTests
{
    private readonly IWeatherIngestionService _ingestion = Substitute.For<IWeatherIngestionService>();

    [Fact]
    public void The_function_is_bound_to_a_timer_that_fires_at_second_zero_of_every_minute()
    {
        var method = typeof(FetchWeatherFunction).GetMethod(nameof(FetchWeatherFunction.RunAsync))!;

        method.GetCustomAttribute<FunctionAttribute>().ShouldNotBeNull().Name.ShouldBe("FetchWeather");
        var trigger = method.GetParameters()[0].GetCustomAttribute<TimerTriggerAttribute>().ShouldNotBeNull();
        trigger.Schedule.ShouldBe("0 */1 * * * *"); // NCRONTAB with seconds: {second} {minute} {hour} {day} {month} {day-of-week}
        trigger.RunOnStartup.ShouldBeFalse();
    }

    [Fact]
    public void The_shipped_configuration_fetches_London_Rome_and_Riga_with_their_country_codes()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = configuration.GetSection(WeatherIngestionOptions.SectionName).Get<WeatherIngestionOptions>()
            .ShouldNotBeNull();

        options.Cities.Select(city => (city.Name, city.CountryCode))
            .ShouldBe([("London", "GB"), ("Rome", "IT"), ("Riga", "LV")]);
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true)
            .ShouldBeTrue();
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
