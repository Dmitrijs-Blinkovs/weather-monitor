using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeatherMonitor.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create the context without starting the web or function host.
/// <c>migrations add</c> never opens this connection; <c>database update</c> targets local SQL Server LocalDB.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WeatherDbContext>
{
    private const string LocalDbConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=WeatherMonitor;Trusted_Connection=True;TrustServerCertificate=True";

    public WeatherDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseSqlServer(LocalDbConnectionString)
            .Options;

        return new WeatherDbContext(options);
    }
}
