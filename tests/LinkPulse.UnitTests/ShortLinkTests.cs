using LinkPulse.Api.Data.Entities;

namespace LinkPulse.UnitTests;

public sealed class ShortLinkTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveShortLink()
    {
        var ownerId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(7);

        var shortLink = new ShortLink(
            ownerId,
            "AbC123",
            "https://example.com/articles/1",
            createdAt,
            expiresAt);

        Assert.NotEqual(Guid.Empty, shortLink.Id);
        Assert.Equal(ownerId, shortLink.OwnerId);
        Assert.Equal("abc123", shortLink.ShortCode);
        Assert.Equal(
            "https://example.com/articles/1",
            shortLink.TargetUrl);
        Assert.Equal(createdAt, shortLink.CreatedAt);
        Assert.Equal(expiresAt, shortLink.ExpiresAt);
        Assert.True(shortLink.IsActive);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyOwnerIdentifier()
    {
        var createdAt = DateTimeOffset.UtcNow;

        var action = () => new ShortLink(
            Guid.Empty,
            "abc123",
            "https://example.com",
            createdAt,
            null);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldRejectInvalidExpiration()
    {
        var createdAt = DateTimeOffset.UtcNow;

        var action = () => new ShortLink(
            Guid.NewGuid(),
            "abc123",
            "https://example.com",
            createdAt,
            createdAt);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Update_ShouldChangeTargetUrlAndExpiration()
    {
        var createdAt = DateTimeOffset.UtcNow;

        var shortLink = new ShortLink(
            Guid.NewGuid(),
            "abc123",
            "https://example.com/old",
            createdAt,
            null);

        var newExpiration = createdAt.AddDays(30);

        shortLink.Update(
            "https://example.com/new",
            newExpiration);

        Assert.Equal(
            "https://example.com/new",
            shortLink.TargetUrl);

        Assert.Equal(
            newExpiration,
            shortLink.ExpiresAt);
    }

    [Fact]
    public void Deactivate_ShouldMarkLinkAsInactive()
    {
        var shortLink = new ShortLink(
            Guid.NewGuid(),
            "abc123",
            "https://example.com",
            DateTimeOffset.UtcNow,
            null);

        shortLink.Deactivate();

        Assert.False(shortLink.IsActive);
    }
}