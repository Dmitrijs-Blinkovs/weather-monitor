namespace WeatherMonitor.Web.Caching;

/// <summary>
/// Output-cache policies for the read API. Every open browser tab polls the API, so identical requests within a short
/// window are answered from memory instead of the database. The client rounds time ranges to whole minutes, which
/// makes the history requests of different users identical and cacheable.
/// </summary>
internal static class ApiCaching
{
    public const string History = "history";
    public const string FetchLog = "fetch-log";
    public const string Readings = "readings";
    public const string AttemptDetails = "attempt-details";
    public const string Cities = "cities";

    public static IServiceCollection AddApiCaching(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            // The query string is part of the cache key by default. Locking (request coalescing) is off: a waiting
            // request would inherit the cancellation of the request it waits for, and browsers abort requests all the
            // time (React StrictMode, navigation), which turned live requests into 500s. Concurrent misses are bounded
            // by the global concurrency limiter instead.
            options.AddPolicy(History, policy => policy.Expire(TimeSpan.FromSeconds(60)).SetLocking(false));
            options.AddPolicy(FetchLog, policy => policy.Expire(TimeSpan.FromSeconds(10)).SetLocking(false));
            options.AddPolicy(Readings, policy => policy.Expire(TimeSpan.FromSeconds(10)).SetLocking(false));
            // A stored attempt never changes; only the retention purge removes it.
            options.AddPolicy(AttemptDetails, policy => policy.Expire(TimeSpan.FromMinutes(5)).SetLocking(false));
            options.AddPolicy(Cities, policy => policy.Expire(TimeSpan.FromMinutes(5)).SetLocking(false));
        });

        // JSON responses carry no secrets or cookies, so compressing them over HTTPS has no BREACH-style risk here.
        services.AddResponseCompression(options => options.EnableForHttps = true);
        return services;
    }
}
