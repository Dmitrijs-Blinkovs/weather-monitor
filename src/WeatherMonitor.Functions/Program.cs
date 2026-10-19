using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WeatherMonitor.Application;
using WeatherMonitor.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Locally the OpenWeatherMap API key lives in user secrets, never in source control (see README).
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services
    .AddWeatherIngestion(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddOpenWeatherMap(builder.Configuration);

await builder.Build().RunAsync();
