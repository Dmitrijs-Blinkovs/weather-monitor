using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

/// <summary>
/// The only tests that need SQL Server: the race they prove exists only there. Skipped unless
/// <see cref="ServerVariable"/> holds a connection string without a database, e.g.
/// <c>Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True</c>. CI sets it in the
/// "Migrations on SQL Server" job. Each test creates and drops its own database.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DatabaseMigrationServiceTests
{
    public const string ServerVariable = "WEATHERMONITOR_TEST_SQLSERVER";

    [Fact]
    public async Task A_database_created_by_another_host_while_this_one_creates_it_is_still_migrated()
    {
        // Web and Functions start together in development: both see no database and both create it, so one of them
        // gets error 1801. The other host is simulated just before this host's CREATE DATABASE, which makes the race
        // deterministic. (Several hosts in one process cannot show it: EF Core clears every connection pool of the
        // process after creating a database, which breaks the other hosts' commands - Web and Functions are separate.)
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server), $"Set {ServerVariable} to run the SQL Server tests.");

        var connectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"WeatherMonitorTest_{Guid.NewGuid():N}",
        }.ConnectionString;
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = BuildHost(connectionString, new OtherHostCreatesTheDatabaseFirst(connectionString));

        try
        {
            await MigrationService(host).StartingAsync(cancellationToken);

            await using var scope = host.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Database;
            (await database.GetAppliedMigrationsAsync(cancellationToken)).ShouldNotBeEmpty();
            (await database.GetPendingMigrationsAsync(cancellationToken)).ShouldBeEmpty();
        }
        finally
        {
            await DropDatabaseAsync(host);
        }
    }

    /// <summary>One host's services, composed as the hosts compose them (retry policy included).</summary>
    private static ServiceProvider BuildHost(string connectionString, IInterceptor interceptor)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
                [DependencyInjection.ApplyMigrationsOnStartupKey] = "true",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddPersistence(configuration)
            .ConfigureDbContext<WeatherDbContext>(options => options.AddInterceptors(interceptor))
            .BuildServiceProvider();
    }

    private static IHostedLifecycleService MigrationService(IServiceProvider host) =>
        host.GetServices<IHostedService>().OfType<DatabaseMigrationService>().Single();

    private static async Task DropDatabaseAsync(IServiceProvider host)
    {
        // After a failed run the pool may still replay "cannot open database", and the drop would think it is gone.
        SqlConnection.ClearAllPools();
        await using var scope = host.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Database
            .EnsureDeletedAsync(CancellationToken.None);
    }

    /// <summary>Creates the database on its own connection right before the host's first CREATE DATABASE runs.</summary>
    private sealed class OtherHostCreatesTheDatabaseFirst(string connectionString) : DbCommandInterceptor
    {
        private int _created;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref _created, 1) == 0)
            {
                var target = new SqlConnectionStringBuilder(connectionString);
                var database = target.InitialCatalog;
                target.InitialCatalog = "master";
                target.Pooling = false;

                await using var connection = new SqlConnection(target.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var create = connection.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{database}]";
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
