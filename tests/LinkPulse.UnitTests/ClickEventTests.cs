using LinkPulse.Api.Data.Entities;

namespace LinkPulse.UnitTests;

public sealed class ClickEventTests
{
    [Fact]
    public void Constructor_ShouldNormalizeOptionalValues()
    {
        var shortLinkId =
            Guid.NewGuid();

        var occurredAt =
            DateTimeOffset.UtcNow;

        var clickEvent = new ClickEvent(
            shortLinkId,
            occurredAt,
            "  https://example.com/source  ",
            "  LinkPulse-Test/1.0  ",
            null);

        Assert.Equal(
            shortLinkId,
            clickEvent.ShortLinkId);

        Assert.Equal(
            occurredAt,
            clickEvent.OccurredAt);

        Assert.Equal(
            "https://example.com/source",
            clickEvent.Referrer);

        Assert.Equal(
            "LinkPulse-Test/1.0",
            clickEvent.UserAgent);

        Assert.Null(
            clickEvent.ClientIpHash);
    }

    [Fact]
    public void Constructor_WithBlankValues_ShouldUseNull()
    {
        var clickEvent = new ClickEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            " ",
            string.Empty,
            null);

        Assert.Null(clickEvent.Referrer);
        Assert.Null(clickEvent.UserAgent);
        Assert.Null(clickEvent.ClientIpHash);
    }
}