using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WeatherMonitor.Application;
using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Ingestion;
using WeatherMonitor.Infrastructure.OpenWeatherMap;

namespace WeatherMonitor.Infrastructure.Tests;

/// <summary>Verifies the dependency graphs the two hosts build, without starting them.</summary>
public sealed class CompositionTests
{
    private static readonly Dictionary<string, string?> FunctionSettings = new()
    {
        ["ConnectionStrings:WeatherDb"] = "Server=(localdb)\\MSSQLLocalDB;Database=Unused",
        ["Weather:Cities:0:Name"] = "London",
        ["Weather:Cities:0:CountryCode"] = "GB",
        ["Weather:Cities:1:Name"] = "Riga",
        ["OpenWeatherMap:ApiKey"] = "key",
    };

    [Fact]
    public void The_function_host_can_resolve_the_ingestion_service()
    {
        using var provider = BuildFunctionServices(FunctionSettings);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IWeatherIngestionService>().ShouldNotBeNull();
        var cities = scope.ServiceProvider.GetRequiredService<IOptions<WeatherIngestionOptions>>().Value.Cities;
        cities.Select(city => (city.Name, city.CountryCode)).ShouldBe([("London", "GB"), ("Riga", null)]);
    }

    [Fact]
    public void A_missing_api_key_is_reported_with_instructions()
    {
        var settings = new Dictionary<string, string?>(FunctionSettings) { ["OpenWeatherMap:ApiKey"] = null };
        using var provider = BuildFunctionServices(settings);

        var exception = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<OpenWeatherMapOptions>>().Value);

        exception.Message.ShouldContain("user-secrets");
    }

    [Fact]
    public void The_web_host_can_resolve_the_history_service()
    {
        var configuration = Configuration(new() { ["ConnectionStrings:WeatherDb"] = "Server=unused" });
        using var provider = new ServiceCollection()
            .AddWeatherHistory()
            .AddPersistence(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IWeatherHistoryService>().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void Migrations_run_on_startup_only_when_enabled(string? setting, bool expected)
    {
        var configuration = Configuration(new() { [DependencyInjection.ApplyMigrationsOnStartupKey] = setting });

        var services = new ServiceCollection().AddPersistence(configuration);

        services.Any(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(expected);
    }

    private static ServiceProvider BuildFunctionServices(Dictionary<string, string?> settings)
    {
        var configuration = Configuration(settings);
        return new ServiceCollection()
            .AddLogging()
            .AddWeatherIngestion(configuration)
            .AddPersistence(configuration)
            .AddOpenWeatherMap(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
