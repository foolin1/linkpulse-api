using LinkPulse.Api.Caching;

namespace LinkPulse.UnitTests;

public sealed class LinkCachePolicyTests
{
    [Fact]
    public void CalculateTtl_WithoutExpiration_ShouldUseDefaultTtl()
    {
        var currentTime =
            new DateTimeOffset(
                2026,
                7,
                26,
                12,
                0,
                0,
                TimeSpan.Zero);

        var defaultTtl =
            TimeSpan.FromMinutes(15);

        var result = LinkCachePolicy.CalculateTtl(
            null,
            currentTime,
            defaultTtl);

        Assert.Equal(defaultTtl, result);
    }

    [Fact]
    public void CalculateTtl_WithNearExpiration_ShouldUseRemainingLifetime()
    {
        var currentTime =
            new DateTimeOffset(
                2026,
                7,
                26,
                12,
                0,
                0,
                TimeSpan.Zero);

        var expiresAt =
            currentTime.AddMinutes(5);

        var result = LinkCachePolicy.CalculateTtl(
            expiresAt,
            currentTime,
            TimeSpan.FromMinutes(15));

        Assert.Equal(
            TimeSpan.FromMinutes(5),
            result);
    }

    [Fact]
    public void CalculateTtl_WithDistantExpiration_ShouldUseDefaultTtl()
    {
        var currentTime =
            new DateTimeOffset(
                2026,
                7,
                26,
                12,
                0,
                0,
                TimeSpan.Zero);

        var expiresAt =
            currentTime.AddHours(2);

        var defaultTtl =
            TimeSpan.FromMinutes(15);

        var result = LinkCachePolicy.CalculateTtl(
            expiresAt,
            currentTime,
            defaultTtl);

        Assert.Equal(defaultTtl, result);
    }

    [Fact]
    public void CalculateTtl_WithExpiredLink_ShouldReturnNull()
    {
        var currentTime =
            new DateTimeOffset(
                2026,
                7,
                26,
                12,
                0,
                0,
                TimeSpan.Zero);

        var expiresAt =
            currentTime.AddMinutes(-1);

        var result = LinkCachePolicy.CalculateTtl(
            expiresAt,
            currentTime,
            TimeSpan.FromMinutes(15));

        Assert.Null(result);
    }

    [Fact]
    public void CalculateTtl_WithInvalidDefaultTtl_ShouldThrow()
    {
        var currentTime =
            DateTimeOffset.UtcNow;

        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    _ = LinkCachePolicy.CalculateTtl(
                        null,
                        currentTime,
                        TimeSpan.Zero);
                });

        Assert.Equal(
            "defaultTtl",
            exception.ParamName);
    }

    [Fact]
    public void CachedLink_IsExpired_ShouldReturnExpectedResult()
    {
        var currentTime =
            new DateTimeOffset(
                2026,
                7,
                26,
                12,
                0,
                0,
                TimeSpan.Zero);

        var cachedLink = new CachedLink(
            Guid.NewGuid(),
            "demo",
            "https://example.com",
            currentTime.AddMinutes(-1));

        Assert.True(
            cachedLink.IsExpired(currentTime));
    }
}