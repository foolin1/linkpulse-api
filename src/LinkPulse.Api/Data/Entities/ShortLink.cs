namespace LinkPulse.Api.Data.Entities;

public sealed class ShortLink
{
    private ShortLink()
    {
    }

    public ShortLink(
        Guid ownerId,
        string shortCode,
        string targetUrl,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Owner identifier cannot be empty.",
                nameof(ownerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(shortCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);

        ValidateExpiration(createdAt, expiresAt);

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        ShortCode = NormalizeShortCode(shortCode);
        TargetUrl = targetUrl.Trim();
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string ShortCode { get; private set; } = string.Empty;

    public string TargetUrl { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive { get; private set; }

    public ApplicationUser Owner { get; private set; } = null!;

    public ICollection<ClickEvent> ClickEvents { get; } = new List<ClickEvent>();

    public void Update(
        string targetUrl,
        DateTimeOffset? expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);

        ValidateExpiration(CreatedAt, expiresAt);

        TargetUrl = targetUrl.Trim();
        ExpiresAt = expiresAt;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeShortCode(string shortCode)
    {
        return shortCode.Trim().ToLowerInvariant();
    }

    private static void ValidateExpiration(
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        if (expiresAt.HasValue && expiresAt.Value <= createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                "Expiration time must be later than creation time.");
        }
    }
}