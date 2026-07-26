namespace LinkPulse.Api.Caching;

public static class LinkCachePolicy
{
    public static TimeSpan? CalculateTtl(
        DateTimeOffset? expiresAt,
        DateTimeOffset currentTime,
        TimeSpan defaultTtl)
    {
        if (defaultTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultTtl),
                "Default cache TTL must be positive.");
        }

        if (!expiresAt.HasValue)
        {
            return defaultTtl;
        }

        var remainingLifetime =
            expiresAt.Value - currentTime;

        if (remainingLifetime <= TimeSpan.Zero)
        {
            return null;
        }

        return remainingLifetime < defaultTtl
            ? remainingLifetime
            : defaultTtl;
    }
}