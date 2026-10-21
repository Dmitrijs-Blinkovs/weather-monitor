using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Web.Tests;

/// <summary>
/// Runs the real ASP.NET Core pipeline (routing, model binding, validation, JSON) in memory,
/// with the application services replaced by substitutes so no database is needed.
/// </summary>
public sealed class WeatherApiFactory : WebApplicationFactory<Program>
{
    public const string SpaIndexHtml = "<!doctype html><title>Weather Monitor</title><div id=\"root\"></div>";

    // Stands in for the React build that is copied into wwwroot on publish.
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("weather-monitor-wwwroot-");

    public WeatherApiFactory()
    {
        File.WriteAllText(Path.Combine(_webRoot.FullName, "index.html"), SpaIndexHtml);
    }

    public IFetchLogReader FetchLogReader { get; } = Substitute.For<IFetchLogReader>();

    public IWeatherHistoryService HistoryService { get; } = Substitute.For<IWeatherHistoryService>();

    public IWeatherReadingsReader ReadingsReader { get; } = Substitute.For<IWeatherReadingsReader>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that environment would point at LocalDB and apply migrations.
        builder.UseEnvironment("Testing");
        builder.UseWebRoot(_webRoot.FullName);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(FetchLogReader);
            services.AddSingleton(HistoryService);
            services.AddSingleton(ReadingsReader);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _webRoot.Delete(recursive: true);
    }
}
