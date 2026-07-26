namespace LinkPulse.Api.Data.Entities;

public sealed class ClickEvent
{
    private ClickEvent()
    {
    }

    public ClickEvent(
        Guid shortLinkId,
        DateTimeOffset occurredAt,
        string? referrer,
        string? userAgent,
        string? clientIpHash)
    {
        if (shortLinkId == Guid.Empty)
        {
            throw new ArgumentException(
                "Short link identifier cannot be empty.",
                nameof(shortLinkId));
        }

        ShortLinkId = shortLinkId;
        OccurredAt = occurredAt;
        Referrer = NormalizeOptionalValue(referrer);
        UserAgent = NormalizeOptionalValue(userAgent);
        ClientIpHash = NormalizeOptionalValue(clientIpHash);
    }

    public long Id { get; private set; }

    public Guid ShortLinkId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string? Referrer { get; private set; }

    public string? UserAgent { get; private set; }

    public string? ClientIpHash { get; private set; }

    public ShortLink ShortLink { get; private set; } = null!;

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}