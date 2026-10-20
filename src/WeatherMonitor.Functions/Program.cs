using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using WeatherMonitor.Application;
using WeatherMonitor.Application.Ingestion;
using WeatherMonitor.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Locally the OpenWeatherMap API key lives in user secrets, never in source control (see README).
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services
    .AddWeatherIngestion(builder.Configuration)
    .AddDataRetention(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddOpenWeatherMap(builder.Configuration);

// OpenTelemetry: function invocations, outgoing HTTP calls (query strings are redacted, so the API key never reaches
// telemetry), the ingestion metrics and the logs, exported to Application Insights. Wired up only when a connection
// string is configured: with UseFunctionsWorkerDefaults the worker reports its own telemetry and the host stops
// forwarding worker logs, so enabling it without an exporter would hide every log line from the local console.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .WithTracing(tracing => tracing.AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics.AddMeter(IngestionMetrics.MeterName).AddHttpClientInstrumentation())
        .UseAzureMonitorExporter();
}

await builder.Build().RunAsync();
