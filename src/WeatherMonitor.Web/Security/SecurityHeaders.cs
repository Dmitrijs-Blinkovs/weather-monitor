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

    /// <summary>Path of the interactive API reference (Scalar), mapped in development only.</summary>
    public const string ApiReferencePath = "/scalar";

    /// <summary>
    /// The API reference page configures itself with inline script and styles, so under its path - and only in
    /// development - inline code is allowed. Everything else still comes from this origin only.
    /// </summary>
    public const string ApiReferenceContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; " +
        "form-action 'self'; frame-ancestors 'none'";

    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    /// <summary>
    /// Adds the headers to every response. With <c>relaxForApiReference</c> (development only), responses under
    /// <see cref="ApiReferencePath"/> get <see cref="ApiReferenceContentSecurityPolicy"/>.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool relaxForApiReference = false) =>
        app.Use(async (context, next) =>
        {
            var policy = relaxForApiReference && context.Request.Path.StartsWithSegments(ApiReferencePath)
                ? ApiReferenceContentSecurityPolicy
                : ContentSecurityPolicy;

            // Added when the response starts, so they survive the exception handler clearing the response for a 500.
            context.Response.OnStarting(
                static state =>
                {
                    var (response, policy) = ((HttpResponse, string))state;
                    var headers = response.Headers;
                    headers.ContentSecurityPolicy = policy;
                    headers.XContentTypeOptions = "nosniff";
                    headers.XFrameOptions = "DENY";
                    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                    headers["Permissions-Policy"] = PermissionsPolicy;
                    headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    headers["Cross-Origin-Resource-Policy"] = "same-origin";
                    return Task.CompletedTask;
                },
                (context.Response, policy));

            await next(context);
        });
}
