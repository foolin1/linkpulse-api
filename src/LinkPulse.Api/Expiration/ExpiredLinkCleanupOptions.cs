namespace LinkPulse.Api.Expiration;

public sealed class ExpiredLinkCleanupOptions
{
    public const string SectionName =
        "ExpirationCleanup";

    public int IntervalSeconds
    {
        get;
        init;
    } = 60;

    public int BatchSize
    {
        get;
        init;
    } = 100;
}