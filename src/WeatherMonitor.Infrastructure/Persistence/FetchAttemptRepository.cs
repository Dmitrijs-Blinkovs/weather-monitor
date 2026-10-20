using Microsoft.EntityFrameworkCore;
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

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        // One set-based DELETE per batch (no entities loaded); snapshots follow through ON DELETE CASCADE.
        // Oldest first along the time index, so a batch stops at the cutoff instead of scanning the whole table.
        return dbContext.FetchAttempts
            .Where(attempt => attempt.AttemptedAtUtc < cutoffUtc)
            .OrderBy(attempt => attempt.AttemptedAtUtc)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
