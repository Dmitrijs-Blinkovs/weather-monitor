using WeatherMonitor.Application.Abstractions;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Infrastructure.Persistence;

internal sealed class FetchAttemptRepository(WeatherDbContext dbContext) : IFetchAttemptRepository
{
    public async Task AddRangeAsync(IReadOnlyCollection<FetchAttempt> attempts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        // Snapshots are owned by their attempts and are inserted with them; SaveChanges runs in one transaction.
        dbContext.FetchAttempts.AddRange(attempts);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
