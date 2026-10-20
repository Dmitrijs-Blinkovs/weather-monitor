using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests.Architecture;

/// <summary>The dependency rule of ADR 0001 as tests: inner layers never reference outer layers or technologies.</summary>
public sealed class DependencyRuleTests
{
    [Fact]
    public void The_domain_depends_on_nothing_but_the_base_class_library()
    {
        var references = typeof(FetchAttempt).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToList();

        references.ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal));
    }

    [Fact]
    public void The_application_layer_knows_no_infrastructure_or_host_technology()
    {
        string[] forbidden =
        [
            "WeatherMonitor.Infrastructure", "WeatherMonitor.Web", "WeatherMonitor.Functions",
            "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient", "Microsoft.AspNetCore",
            "Microsoft.Azure.Functions", "System.Net.Http",
        ];

        var references = typeof(IWeatherProvider).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToList();

        references.ShouldNotContain(name => forbidden.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
