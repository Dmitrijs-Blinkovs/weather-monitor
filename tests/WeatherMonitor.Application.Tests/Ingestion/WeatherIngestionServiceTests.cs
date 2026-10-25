using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Application.Ingestion;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests.Ingestion;

public sealed class WeatherIngestionServiceTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

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
    public async Task A_failed_city_keeps_the_providers_response_body()
    {
        _provider.GetCurrentWeatherAsync("Rome", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(WeatherFetchResult.Failure(200, "Response body is not valid JSON", "<html>maintenance</html>"));

        await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        _stored.Single(attempt => attempt.City == "Rome").ResponseBody.ShouldBe("<html>maintenance</html>");
    }

    [Fact]
    public async Task An_unexpected_provider_exception_is_logged_as_a_failure_instead_of_aborting_the_run()
    {
        _provider.GetCurrentWeatherAsync("London", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<WeatherFetchResult>(_ => throw new InvalidOperationException("boom: appid=secret"));

        var summary = await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        var london = _stored.Single(attempt => attempt.City == "London");
        london.IsSuccess.ShouldBeFalse();
        london.HttpStatusCode.ShouldBeNull();
        summary.Failed.ShouldBe(1);

        // Only the exception type is stored; its text (which might carry request details) stays in the logs.
        london.ErrorMessage.ShouldBe("Unexpected error (InvalidOperationException); see the function logs.");
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
    public async Task A_cancelled_run_keeps_finished_cities_logs_unfinished_ones_as_failures_and_reports_the_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var saveToken = CancellationToken.None;
        _repository
            .AddRangeAsync(Arg.Do<IReadOnlyCollection<FetchAttempt>>(attempts => _stored = attempts), Arg.Do<CancellationToken>(token => saveToken = token))
            .Returns(Task.CompletedTask);
        _provider.GetCurrentWeatherAsync("Rome", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return WeatherFetchResult.Success(200, TestData.Weather());
            });

        var run = CreateService().IngestAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => run);
        _stored.Count.ShouldBe(3);
        _stored.Where(attempt => attempt.City != "Rome").ShouldAllBe(attempt => attempt.IsSuccess);
        var rome = _stored.Single(attempt => attempt.City == "Rome");
        rome.IsSuccess.ShouldBeFalse();
        rome.ErrorMessage.ShouldBe(WeatherIngestionService.CancelledMessage);
        saveToken.IsCancellationRequested.ShouldBeFalse(); // saved with its own short timeout, not the cancelled token
    }

    [Fact]
    public async Task A_cancellation_that_arrives_while_saving_still_stores_the_run_and_is_then_reported()
    {
        using var cancellation = new CancellationTokenSource();
        var saves = 0;
        _repository
            .AddRangeAsync(Arg.Any<IReadOnlyCollection<FetchAttempt>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                // The host starts shutting down while the database is still writing; a real save honours its token.
                await cancellation.CancelAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(50), call.Arg<CancellationToken>());
                _stored = call.Arg<IReadOnlyCollection<FetchAttempt>>();
                saves++;
            });

        await Should.ThrowAsync<OperationCanceledException>(() => CreateService().IngestAsync(cancellation.Token));

        saves.ShouldBe(1);
        _stored.Count.ShouldBe(3);
        _stored.ShouldAllBe(attempt => attempt.IsSuccess);
    }

    [Fact]
    public async Task Every_attempt_is_counted_per_city_and_outcome_for_monitoring()
    {
        var meterFactory = _services.GetRequiredService<IMeterFactory>();
        using var attempts = new MetricCollector<long>(meterFactory, IngestionMetrics.MeterName, "weather.fetch.attempts");
        _provider.GetCurrentWeatherAsync("Rome", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(WeatherFetchResult.Failure(401, "Invalid API key"));

        await CreateService().IngestAsync(TestContext.Current.CancellationToken);

        var measurements = attempts.GetMeasurementSnapshot();
        measurements.Count.ShouldBe(3);
        measurements.ShouldContain(m => (string?)m.Tags["city"] == "Rome" && (string?)m.Tags["outcome"] == "failure");
        measurements.Count(m => (string?)m.Tags["outcome"] == "success").ShouldBe(2);
    }

    [Fact]
    public async Task A_run_the_database_rejects_is_logged_as_an_error_and_not_counted_as_stored_data()
    {
        // The "no weather data" alert counts successful attempts; they must only count once they are stored.
        var meterFactory = _services.GetRequiredService<IMeterFactory>();
        using var attempts = new MetricCollector<long>(meterFactory, IngestionMetrics.MeterName, "weather.fetch.attempts");
        var logger = new FakeLogger<WeatherIngestionService>();
        _repository.AddRangeAsync(Arg.Any<IReadOnlyCollection<FetchAttempt>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("String or binary data would be truncated."));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateService(logger: logger).IngestAsync(TestContext.Current.CancellationToken));

        attempts.GetMeasurementSnapshot().ShouldBeEmpty();
        logger.Collector.GetSnapshot().ShouldContain(record =>
            record.Level == LogLevel.Error && record.Exception is InvalidOperationException);
    }

    public void Dispose() => _services.Dispose();

    private WeatherIngestionService CreateService(
        CityOptions[]? cities = null,
        ILogger<WeatherIngestionService>? logger = null) =>
        new(
            _provider,
            _repository,
            Options.Create(new WeatherIngestionOptions { Cities = [.. cities ?? Cities] }),
            _timeProvider,
            new IngestionMetrics(_services.GetRequiredService<IMeterFactory>()),
            logger ?? NullLogger<WeatherIngestionService>.Instance);
}
