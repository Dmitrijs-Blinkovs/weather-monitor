using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using WeatherMonitor.Application;
using WeatherMonitor.Infrastructure;
using WeatherMonitor.Web.Caching;
using WeatherMonitor.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddWeatherHistory()
    .AddPersistence(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Weather Monitor API";
    document.Info.Description =
        "Read API of Weather Monitor: the fetch log, the temperature history for the chart and every stored reading. "
        + "Timestamps are UTC; ranges are ISO 8601 with an offset and at most 31 days.";
    return Task.CompletedTask;
}));
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiCaching();
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
builder.Services.AddHealthChecks().AddPersistenceHealthChecks();

// OpenTelemetry (requests, SQL, HTTP, logs) to Application Insights when a connection string is configured (Azure).
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders(relaxForApiReference: app.Environment.IsDevelopment());

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

// Serves the React build that is copied into wwwroot on publish.
// In development the SPA proxy forwards the browser to the Vite dev server instead.
app.UseDefaultFiles();
app.MapStaticAssets();

app.UseRateLimiter();
app.UseOutputCache();

app.MapControllers().RequireRateLimiting(ApiRateLimiting.PolicyName);

// Liveness (App Service health check): the process answers; no dependencies, so a database blip never recycles it.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: database reachable and data fresh (Degraded when no fetch succeeded recently). Used by the smoke test.
app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(WeatherMonitor.Infrastructure.DependencyInjection.ReadinessTag),
    })
    .RequireRateLimiting(ApiRateLimiting.PolicyName);

// The API description and its interactive reference exist only in development: /openapi/v1.json and /scalar.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(SecurityHeaders.ApiReferencePath, options => options
        .WithTitle("Weather Monitor API")
        .DisableDefaultFonts()
        // A local, read-only reference: no Scalar cloud features, AI agent, MCP or telemetry.
        .HideDeveloperTools()
        .DisableAgent()
        .DisableMcp()
        .DisableTelemetry());
}

// Unknown API routes are real 404s; every other unknown path is a client-side route of the SPA.
// GET/HEAD only, so a wrong verb on an existing route (e.g. POST /api/logs) still gets 405.
app.MapMethods(
        "/api/{**path}",
        [HttpMethods.Get, HttpMethods.Head],
        () => Results.Problem(statusCode: StatusCodes.Status404NotFound))
    .RequireRateLimiting(ApiRateLimiting.PolicyName)
    .ExcludeFromDescription();
app.MapFallbackToFile("/index.html");

await app.RunAsync();
