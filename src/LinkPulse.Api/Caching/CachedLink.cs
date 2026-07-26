namespace LinkPulse.Api.Caching;

public sealed record CachedLink(
    Guid Id,
    string ShortCode,
    string TargetUrl,
    DateTimeOffset? ExpiresAt)
{
    public bool IsExpired(DateTimeOffset currentTime)
    {
        return ExpiresAt.HasValue
            && ExpiresAt.Value <= currentTime;
    }
}