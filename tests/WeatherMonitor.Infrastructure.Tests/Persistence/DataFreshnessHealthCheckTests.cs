using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

public sealed class DataFreshnessHealthCheckTests : SqliteTestDatabase
{
    [Fact]
    public async Task Without_any_stored_data_it_is_degraded()
    {
        var result = await CheckAsync();

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Every_city_with_a_recent_success_makes_it_healthy()
    {
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Success("Riga", Now.AddMinutes(-2)),
            Success("Rome", Now.AddMinutes(-3)));

        var result = await CheckAsync();

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task One_city_without_a_recent_success_makes_it_degraded_even_when_the_others_are_fine()
    {
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Success("Riga", Now.AddMinutes(-1)),
            Success("Rome", Now.AddMinutes(-30)),
            Failure("Rome", Now.AddMinutes(-1)));

        var result = await CheckAsync();

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Rome");
        result.Description.ShouldNotContain("London");
    }

    [Fact]
    public async Task A_city_that_only_ever_failed_today_makes_it_degraded()
    {
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Failure("Atlantis", Now.AddMinutes(-1)));

        var result = await CheckAsync();

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Atlantis");
    }

    [Fact]
    public async Task A_city_not_fetched_for_a_day_is_no_longer_expected()
    {
        await SeedAsync(
            Success("London", Now.AddMinutes(-1)),
            Success("Oslo", Now.AddHours(-25)));

        var result = await CheckAsync();

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    private async Task<HealthCheckResult> CheckAsync()
    {
        await using var context = CreateContext();
        var check = new DataFreshnessHealthCheck(context, new FakeTimeProvider(new DateTimeOffset(Now)));
        return await check.CheckHealthAsync(
            new HealthCheckContext { Registration = new HealthCheckRegistration("data", check, null, null) },
            TestContext.Current.CancellationToken);
    }
}
