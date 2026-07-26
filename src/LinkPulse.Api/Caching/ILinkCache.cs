namespace LinkPulse.Api.Caching;

public interface ILinkCache
{
    Task<LinkCacheLookupResult> GetAsync(
        string shortCode,
        CancellationToken cancellationToken);

    Task SetAsync(
        CachedLink cachedLink,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        string shortCode,
        CancellationToken cancellationToken);
}

public enum LinkCacheLookupStatus
{
    Hit,
    Miss,
    Unavailable
}

public sealed record LinkCacheLookupResult(
    LinkCacheLookupStatus Status,
    CachedLink? CachedLink);