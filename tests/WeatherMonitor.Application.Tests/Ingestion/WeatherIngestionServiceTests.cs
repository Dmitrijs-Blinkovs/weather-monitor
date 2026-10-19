using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Application.Ingestion;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests.Ingestion;

public sealed class WeatherIngestionServiceTests
{
    private static readonly CityOptions[] Cities =
    [
        new() { Name = "London", CountryCode = "GB" },
        new() { Name = "Rome", CountryCode = "IT" },
        new() { Name = "Riga", CountryCode = "LV" },
    ];

    private readonly IWeatherProvider _provider = Substitute.For<IWeatherProvider>();
    private readonly IFetchAttemptRepository _repository = Substitute.For<IFetchAttemptRepository>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(TestData.Now));
    private IReadOnlyCollection<FetchAttempt> _stored = [];

    public WeatherIngestionServiceTests()
    {
        _repository
            .AddRangeAsync(Arg.Do<IReadOnlyCollection<FetchAttempt>>(attempts => _stored = attempts), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _provider
            .GetCurrentWeatherAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(WeatherFetchResult.Success(200, TestData.Weather()));
    }

    [Fact]
    public async Task Stores_one_attempt_per_configured_city_in_a_single_call()
    {
        var summary = await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        await _repository.Received(1).AddRangeAsync(Arg.Any<IReadOnlyCollection<FetchAttempt>>(), Arg.Any<CancellationToken>());
        _stored.Select(attempt => attempt.City).ShouldBe(["London", "Rome", "Riga"], ignoreOrder: true);
        summary.ShouldBe(new IngestionSummary(Succeeded: 3, Failed: 0));
    }

    [Fact]
    public async Task Each_city_is_requested_with_its_country_code_so_the_name_is_unambiguous()
    {
        await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        await _provider.Received(1).GetCurrentWeatherAsync("Rome", "IT", Arg.Any<CancellationToken>());
        await _provider.Received(1).GetCurrentWeatherAsync("Riga", "LV", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_successful_fetch_is_stored_with_its_snapshot_and_fetch_time()
    {
        var weather = TestData.Weather(temperature: 18.2, country: "IT");
        _provider.GetCurrentWeatherAsync("Rome", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(WeatherFetchResult.Success(200, weather));

        await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        var rome = _stored.Single(attempt => attempt.City == "Rome");
        rome.IsSuccess.ShouldBeTrue();
        rome.AttemptedAtUtc.ShouldBe(TestData.Now);
        rome.Snapshot.ShouldNotBeNull().Temperature.ShouldBe(18.2);
        rome.Snapshot.RawPayload.ShouldBe(weather.RawPayload);
    }

    [Fact]
    public async Task A_failed_city_is_logged_as_a_failure_while_the_others_are_still_stored()
    {
        _provider.GetCurrentWeatherAsync("Rome", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(WeatherFetchResult.Failure(404, "city not found"));

        var summary = await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        var rome = _stored.Single(attempt => attempt.City == "Rome");
        rome.IsSuccess.ShouldBeFalse();
        rome.HttpStatusCode.ShouldBe(404);
        rome.ErrorMessage.ShouldBe("city not found");
        rome.Snapshot.ShouldBeNull();

        _stored.Where(attempt => attempt.City != "Rome").ShouldAllBe(attempt => attempt.IsSuccess);
        summary.ShouldBe(new IngestionSummary(Succeeded: 2, Failed: 1));
    }

    [Fact]
    public async Task An_unexpected_provider_exception_is_logged_as_a_failure_instead_of_aborting_the_run()
    {
        _provider.GetCurrentWeatherAsync("London", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<WeatherFetchResult>(_ => throw new InvalidOperationException("boom"));

        var summary = await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        var london = _stored.Single(attempt => attempt.City == "London");
        london.IsSuccess.ShouldBeFalse();
        london.HttpStatusCode.ShouldBeNull();
        london.ErrorMessage.ShouldBe("boom");
        summary.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task The_duration_of_each_call_is_measured_with_the_time_provider()
    {
        _provider.GetCurrentWeatherAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
                return WeatherFetchResult.Success(200, TestData.Weather());
            });

        await CreateService([new CityOptions { Name = "London" }]).IngestAsync(TestContext.Current.CancellationToken);

        _stored.Single().DurationMs.ShouldBe(250);
    }

    [Fact]
    public async Task Cancellation_stops_the_run_without_storing_anything()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _provider.GetCurrentWeatherAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<WeatherFetchResult>(call => throw new OperationCanceledException(call.Arg<CancellationToken>()));

        await Should.ThrowAsync<OperationCanceledException>(() => CreateService().IngestAsync(cancellation.Token));

        await _repository.DidNotReceive()
            .AddRangeAsync(Arg.Any<IReadOnlyCollection<FetchAttempt>>(), Arg.Any<CancellationToken>());
    }

    private WeatherIngestionService CreateService(CityOptions[]? cities = null) =>
        new(
            _provider,
            _repository,
            Options.Create(new WeatherIngestionOptions { Cities = [.. cities ?? Cities] }),
            _timeProvider,
            NullLogger<WeatherIngestionService>.Instance);
}
