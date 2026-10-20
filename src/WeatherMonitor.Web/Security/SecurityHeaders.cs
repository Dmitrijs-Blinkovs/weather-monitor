namespace WeatherMonitor.Web.Security;

/// <summary>
/// Browser hardening headers (OWASP Secure Headers Project). The production React build has no inline scripts or
/// styles and calls only its own origin, so the content security policy can be strict.
/// The browser tests run against `vite preview`, which sends the same headers (vite.config.ts) - keep them in sync.
/// </summary>
internal static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Added when the response starts, so they survive the exception handler clearing the response for a 500.
            context.Response.OnStarting(
                static state =>
                {
                    var headers = ((HttpContext)state).Response.Headers;
                    headers.ContentSecurityPolicy = ContentSecurityPolicy;
                    headers.XContentTypeOptions = "nosniff";
                    headers.XFrameOptions = "DENY";
                    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                    headers["Permissions-Policy"] = PermissionsPolicy;
                    headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    headers["Cross-Origin-Resource-Policy"] = "same-origin";
                    return Task.CompletedTask;
                },
                context);

            await next(context);
        });
}
