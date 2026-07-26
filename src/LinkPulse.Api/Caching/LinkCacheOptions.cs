namespace LinkPulse.Api.Caching;

public sealed class LinkCacheOptions
{
    public const string SectionName = "LinkCache";

    public string KeyPrefix { get; init; } =
        "linkpulse:links";

    public int DefaultTtlMinutes { get; init; } = 15;
}