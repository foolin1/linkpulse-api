namespace LinkPulse.Api.RateLimiting;

public sealed class LinkPulseRateLimitOptions
{
    public const string SectionName =
        "RateLimits";

    public int LinkCreationPermitLimit
    {
        get;
        init;
    } = 10;

    public int LinkCreationWindowSeconds
    {
        get;
        init;
    } = 60;

    public int RedirectPermitLimit
    {
        get;
        init;
    } = 60;

    public int RedirectWindowSeconds
    {
        get;
        init;
    } = 60;
}