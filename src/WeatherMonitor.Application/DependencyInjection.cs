using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Ingestion;
using WeatherMonitor.Application.Retention;

namespace WeatherMonitor.Application;

public static class DependencyInjection
{
    /// <summary>Registers the read-side use cases used by the web API.</summary>
    public static IServiceCollection AddWeatherHistory(this IServiceCollection services)
    {
        services.AddScoped<IWeatherHistoryService, WeatherHistoryService>();
        return services;
    }

    /// <summary>Registers the ingestion use case used by the Azure Function.</summary>
    public static IServiceCollection AddWeatherIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WeatherIngestionOptions>()
            .Bind(configuration.GetSection(WeatherIngestionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<IngestionMetrics>();
        services.AddScoped<IWeatherIngestionService, WeatherIngestionService>();
        return services;
    }

    /// <summary>Registers the purge of history older than <c>Retention:Days</c> (used by the Azure Function).</summary>
    public static IServiceCollection AddDataRetention(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DataRetentionOptions>()
            .Bind(configuration.GetSection(DataRetentionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDataRetentionService, DataRetentionService>();
        return services;
    }
}
