using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WeatherMonitor.Web.Security;

/// <summary>
/// Per-client rate limit for the API (OWASP API4: unrestricted resource consumption). The UI needs only a few requests
/// per minute (the chart refreshes every 60 s, the log every 30 s), so the default leaves plenty of headroom.
/// Behind Azure App Service the client address comes from X-Forwarded-For (forwarded headers are enabled there).
/// </summary>
internal static class ApiRateLimiting
{
    public const string PolicyName = "api";
    public const string PermitsPerMinuteKey = "RateLimiting:PermitsPerMinute";
    public const int DefaultPermitsPerMinute = 120;

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue(PermitsPerMinuteKey, DefaultPermitsPerMinute);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests")
                    .ExecuteAsync(context.HttpContext);
            };

            // Caps concurrent work across all clients, so a burst from many addresses cannot exhaust the small
            // database the ingestion function also writes to.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter(
                    "global",
                    _ => new ConcurrencyLimiterOptions { PermitLimit = 64, QueueLimit = 128 }));

            options.AddPolicy(PolicyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
