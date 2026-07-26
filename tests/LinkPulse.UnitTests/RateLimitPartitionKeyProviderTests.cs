using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using LinkPulse.Api.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace LinkPulse.UnitTests;

public sealed class RateLimitPartitionKeyProviderTests
{
    [Fact]
    public void ForLinkCreation_WithAuthenticatedUser_ShouldUseUserId()
    {
        var userId = Guid.NewGuid();

        var httpContext =
            new DefaultHttpContext
            {
                User =
                    new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [
                                new Claim(
                                    JwtRegisteredClaimNames.Sub,
                                    userId.ToString())
                            ],
                            "Test"))
            };

        var partitionKey =
            RateLimitPartitionKeyProvider
                .ForLinkCreation(httpContext);

        Assert.Equal(
            $"user:{userId}",
            partitionKey);
    }

    [Fact]
    public void ForLinkCreation_WithoutUser_ShouldUseIpAddress()
    {
        var httpContext =
            new DefaultHttpContext();

        httpContext.Connection.RemoteIpAddress =
            IPAddress.Parse("192.0.2.10");

        var partitionKey =
            RateLimitPartitionKeyProvider
                .ForLinkCreation(httpContext);

        Assert.Equal(
            "ip:192.0.2.10",
            partitionKey);
    }

    [Fact]
    public void ForRedirect_ShouldUseIpAddress()
    {
        var httpContext =
            new DefaultHttpContext();

        httpContext.Connection.RemoteIpAddress =
            IPAddress.Loopback;

        var partitionKey =
            RateLimitPartitionKeyProvider
                .ForRedirect(httpContext);

        Assert.Equal(
            "ip:127.0.0.1",
            partitionKey);
    }
}