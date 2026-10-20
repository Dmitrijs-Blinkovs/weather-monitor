using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
builder.Services.AddOpenApi();
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
app.UseSecurityHeaders();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Unknown API routes are real 404s; every other unknown path is a client-side route of the SPA.
// GET/HEAD only, so a wrong verb on an existing route (e.g. POST /api/logs) still gets 405.
app.MapMethods(
        "/api/{**path}",
        [HttpMethods.Get, HttpMethods.Head],
        () => Results.Problem(statusCode: StatusCodes.Status404NotFound))
    .RequireRateLimiting(ApiRateLimiting.PolicyName);
app.MapFallbackToFile("/index.html");

await app.RunAsync();
