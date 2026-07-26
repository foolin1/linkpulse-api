using LinkPulse.Api.Caching;
using LinkPulse.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkPulse.Api.Expiration;

public sealed class ExpiredLinkProcessor(
    LinkPulseDbContext dbContext,
    ILinkCache linkCache,
    IOptions<ExpiredLinkCleanupOptions> options)
    : IExpiredLinkProcessor
{
    private readonly int batchSize =
        options.Value.BatchSize;

    public async Task<int> ProcessAsync(
        DateTimeOffset currentTime,
        CancellationToken cancellationToken)
    {
        var totalProcessed = 0;

        while (true)
        {
            var expiredLinks =
                await dbContext.ShortLinks
                    .Where(
                        shortLink =>
                            shortLink.IsActive
                            && shortLink.ExpiresAt
                                .HasValue
                            && shortLink.ExpiresAt
                                .Value
                            <= currentTime)
                    .OrderBy(
                        shortLink =>
                            shortLink.ExpiresAt)
                    .Take(batchSize)
                    .ToListAsync(
                        cancellationToken);

            if (expiredLinks.Count == 0)
            {
                break;
            }

            foreach (var shortLink
                     in expiredLinks)
            {
                shortLink.Deactivate();
            }

            await dbContext.SaveChangesAsync(
                cancellationToken);

            var shortCodes = expiredLinks
                .Select(
                    shortLink =>
                        shortLink.ShortCode)
                .ToArray();

            totalProcessed +=
                expiredLinks.Count;

            dbContext.ChangeTracker.Clear();

            foreach (var shortCode in shortCodes)
            {
                await linkCache.RemoveAsync(
                    shortCode,
                    cancellationToken);
            }

            if (expiredLinks.Count < batchSize)
            {
                break;
            }
        }

        return totalProcessed;
    }
}