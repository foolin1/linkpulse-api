namespace LinkPulse.Api.Data.Entities;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(
        string email,
        string normalizedEmail,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        Id = Guid.NewGuid();
        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public ICollection<ShortLink> ShortLinks { get; } =
        new List<ShortLink>();

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }
}