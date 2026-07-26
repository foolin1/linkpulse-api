namespace LinkPulse.IntegrationTests;

public sealed class RateLimitWebApplicationFactory
    : LinkPulseWebApplicationFactory
{
    protected override void CustomizeSettings(
        IDictionary<string, string?> settings)
    {
        settings[
            "RateLimits:LinkCreationPermitLimit"] =
            "1";

        settings[
            "RateLimits:LinkCreationWindowSeconds"] =
            "300";

        settings[
            "RateLimits:RedirectPermitLimit"] =
            "1";

        settings[
            "RateLimits:RedirectWindowSeconds"] =
            "300";
    }
}