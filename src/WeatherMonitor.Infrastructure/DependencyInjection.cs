using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.History;
using WeatherMonitor.Infrastructure.OpenWeatherMap;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "WeatherDb";
    public const string ApplyMigrationsOnStartupKey = "Database:ApplyMigrationsOnStartup";

    /// <summary>Registers the SQL Server database context, repositories and readers.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WeatherDbContext>(options =>
        {
            // Resolved lazily, so hosts that never touch the database (e.g. tests) do not need it configured.
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured.");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IFetchAttemptRepository, FetchAttemptRepository>();
        services.AddScoped<IFetchLogReader, FetchLogReader>();
        services.AddScoped<IWeatherHistoryReader, WeatherHistoryReader>();

        if (configuration.GetValue<bool>(ApplyMigrationsOnStartupKey))
        {
            services.AddHostedService<DatabaseMigrationService>();
        }

        return services;
    }

    /// <summary>Registers the OpenWeatherMap client as the <see cref="IWeatherProvider"/>.</summary>
    public static IServiceCollection AddOpenWeatherMap(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenWeatherMapOptions>()
            .Bind(configuration.GetSection(OpenWeatherMapOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IWeatherProvider, OpenWeatherMapClient>((serviceProvider, client) =>
            {
                var baseUrl = serviceProvider.GetRequiredService<IOptions<OpenWeatherMapOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            })
            // The API key travels in the query string, and the default HttpClient logging writes full URIs.
            .RemoveAllLoggers()
            .AddStandardResilienceHandler(resilience =>
            {
                // A run is scheduled every minute, so the whole call (with retries) must finish well before the next one.
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                resilience.Retry.MaxRetryAttempts = 2;
            });

        return services;
    }
}
