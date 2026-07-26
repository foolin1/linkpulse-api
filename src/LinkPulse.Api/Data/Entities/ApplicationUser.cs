namespace LinkPulse.Api.Data.Entities;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(
        string email,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Id = Guid.NewGuid();
        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public ICollection<ShortLink> ShortLinks { get; } = new List<ShortLink>();
}