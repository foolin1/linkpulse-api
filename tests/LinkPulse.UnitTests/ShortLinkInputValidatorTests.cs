using LinkPulse.Api.Features.Links;

namespace LinkPulse.UnitTests;

public sealed class ShortLinkInputValidatorTests
{
    [Fact]
    public void ValidateCreate_ShouldAcceptValidRequest()
    {
        var currentTime = DateTimeOffset.UtcNow;

        var request = new CreateShortLinkRequest(
            "https://example.com/articles/1",
            "my-link",
            currentTime.AddDays(7));

        var errors =
            ShortLinkInputValidator.ValidateCreate(
                request,
                currentTime);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_ShouldRejectUnsupportedScheme()
    {
        var currentTime = DateTimeOffset.UtcNow;

        var request = new CreateShortLinkRequest(
            "ftp://example.com/file",
            null,
            null);

        var errors =
            ShortLinkInputValidator.ValidateCreate(
                request,
                currentTime);

        Assert.Contains("targetUrl", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_ShouldRejectReservedAlias()
    {
        var currentTime = DateTimeOffset.UtcNow;

        var request = new CreateShortLinkRequest(
            "https://example.com",
            "api",
            null);

        var errors =
            ShortLinkInputValidator.ValidateCreate(
                request,
                currentTime);

        Assert.Contains("customAlias", errors.Keys);
    }

    [Fact]
    public void ValidateCreate_ShouldRejectPastExpiration()
    {
        var currentTime = DateTimeOffset.UtcNow;

        var request = new CreateShortLinkRequest(
            "https://example.com",
            null,
            currentTime.AddMinutes(-1));

        var errors =
            ShortLinkInputValidator.ValidateCreate(
                request,
                currentTime);

        Assert.Contains("expiresAt", errors.Keys);
    }
}