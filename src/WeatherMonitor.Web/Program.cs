using System.Text.Json.Serialization;
using WeatherMonitor.Application;
using WeatherMonitor.Infrastructure;
using WeatherMonitor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddWeatherHistory()
    .AddPersistence(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<WeatherDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serves the React build that is copied into wwwroot on publish.
// In development the SPA proxy forwards the browser to the Vite dev server instead.
app.UseDefaultFiles();
app.MapStaticAssets();

app.MapControllers();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Unknown API routes are real 404s; every other unknown path is a client-side route of the SPA.
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));
app.MapFallbackToFile("/index.html");

await app.RunAsync();
