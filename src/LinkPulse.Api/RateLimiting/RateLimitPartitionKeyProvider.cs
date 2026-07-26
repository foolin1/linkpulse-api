using System.IdentityModel.Tokens.Jwt;

namespace LinkPulse.Api.RateLimiting;

public static class RateLimitPartitionKeyProvider
{
    public static string ForLinkCreation(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        var userId = httpContext.User.FindFirst(
            JwtRegisteredClaimNames.Sub)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        return ForIpAddress(httpContext);
    }

    public static string ForRedirect(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        return ForIpAddress(httpContext);
    }

    private static string ForIpAddress(
        HttpContext httpContext)
    {
        var remoteIpAddress =
            httpContext.Connection
                .RemoteIpAddress?
                .ToString();

        return string.IsNullOrWhiteSpace(
            remoteIpAddress)
            ? "ip:unknown"
            : $"ip:{remoteIpAddress}";
    }
}