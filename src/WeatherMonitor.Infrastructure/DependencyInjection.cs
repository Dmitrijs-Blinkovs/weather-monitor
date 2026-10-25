using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.History;
using WeatherMonitor.Domain;
using WeatherMonitor.Infrastructure.OpenWeatherMap;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "WeatherDb";
    /// <summary>Apply pending EF Core migrations when the host starts; read while composing the services.</summary>
    public const string ApplyMigrationsOnStartupKey = "Database:ApplyMigrationsOnStartup";

    /// <summary>
    /// A current-weather response is about 500 bytes; anything far larger is refused, not buffered. Equal to the
    /// domain's payload limit (a UTF-8 body never has more characters than bytes), so an accepted body always fits.
    /// </summary>
    public const long MaxWeatherResponseBytes = CurrentWeather.RawPayloadMaxLength;

    /// <summary>Registers the SQL Server database context, repositories and readers.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WeatherDbContext>(options =>
        {
            // Resolved lazily, so hosts that never touch the database (e.g. tests) do not need it configured.
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured.");

            // Few, short retries: a run must finish within the function timeout (55 s) even when SQL is flaky.
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null));
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IFetchAttemptRepository, FetchAttemptRepository>();
        services.AddScoped<IFetchLogReader, FetchLogReader>();
        services.AddScoped<IWeatherHistoryReader, WeatherHistoryReader>();
        services.AddScoped<IWeatherReadingsReader, WeatherReadingsReader>();

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        if (configuration.GetValue<bool>(ApplyMigrationsOnStartupKey))
        {
            services.AddHostedService<DatabaseMigrationService>();
        }

        return services;
    }

    /// <summary>Tag of the checks that decide whether the app can serve data (database reachable, data fresh).</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Adds the database check and the data-freshness check, both tagged <see cref="ReadinessTag"/>.</summary>
    public static IHealthChecksBuilder AddPersistenceHealthChecks(this IHealthChecksBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddDbContextCheck<WeatherDbContext>("database", tags: [ReadinessTag])
            .AddCheck<DataFreshnessHealthCheck>("data-freshness", HealthStatus.Degraded, [ReadinessTag]);
    }

    /// <summary>Registers the OpenWeatherMap client as the <see cref="IWeatherProvider"/>.</summary>
    public static IServiceCollection AddOpenWeatherMap(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenWeatherMapOptions>()
            .Bind(configuration.GetSection(OpenWeatherMapOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var weatherClient = services.AddHttpClient<IWeatherProvider, OpenWeatherMapClient>((serviceProvider, client) =>
            {
                var baseUrl = serviceProvider.GetRequiredService<IOptions<OpenWeatherMapOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                client.MaxResponseContentBufferSize = MaxWeatherResponseBytes;
            })
            // The API key travels in the query string, and the default HttpClient logging writes full URIs.
            .RemoveAllLoggers();

        weatherClient.AddStandardResilienceHandler().Configure((resilience, serviceProvider) =>
        {
            // A run is scheduled every minute, so the whole call (with retries) must finish well before the next one:
            // up to three attempts share the request budget, each capped at a third (30 s by default, 10 s each).
            var requestTimeout = serviceProvider.GetRequiredService<IOptions<OpenWeatherMapOptions>>().Value.RequestTimeout;
            resilience.TotalRequestTimeout.Timeout = requestTimeout;
            resilience.AttemptTimeout.Timeout = requestTimeout / 3;
            resilience.Retry.MaxRetryAttempts = 2;

            // Tuned for a handful of calls per minute (the default needs 100 calls per 30 s and never opens):
            // after two failing runs the circuit opens, the next run fails fast, the one after probes again.
            resilience.CircuitBreaker.MinimumThroughput = 6;
            resilience.CircuitBreaker.FailureRatio = 0.5;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(45);
        });

        // Register last: the standard handler disables HttpClient.Timeout. Restore it to the same budget so it also
        // covers reading the response body, which happens outside that handler after the headers arrive.
        weatherClient.ConfigureHttpClient((serviceProvider, client) =>
            client.Timeout = serviceProvider.GetRequiredService<IOptions<OpenWeatherMapOptions>>().Value.RequestTimeout);

        return services;
    }
}
