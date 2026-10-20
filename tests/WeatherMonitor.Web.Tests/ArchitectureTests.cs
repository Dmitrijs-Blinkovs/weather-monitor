using Microsoft.AspNetCore.Mvc;
using WeatherMonitor.Application.History;

namespace WeatherMonitor.Web.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Controllers_depend_only_on_application_ports_never_on_the_database()
    {
        // WeatherDbContext is public and referenced by the host for composition; a controller must still not use it.
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)))
            .ToList();

        var dependencies = controllers
            .SelectMany(controller => controller.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        controllers.ShouldNotBeEmpty();
        dependencies.ShouldAllBe(type => type.Assembly == typeof(IWeatherHistoryService).Assembly);
    }
}
