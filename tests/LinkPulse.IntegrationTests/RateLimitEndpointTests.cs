using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkPulse.IntegrationTests;

public sealed class RateLimitEndpointTests
    : IClassFixture<
        RateLimitWebApplicationFactory>
{
    private readonly HttpClient client;

    public RateLimitEndpointTests(
        RateLimitWebApplicationFactory factory)
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Redirect_WhenLimitIsExceeded_ShouldReturnTooManyRequests()
    {
        using var firstResponse =
            await client.GetAsync(
                "/invalid!");

        Assert.Equal(
            HttpStatusCode.NotFound,
            firstResponse.StatusCode);

        using var secondResponse =
            await client.GetAsync(
                "/invalid!");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            secondResponse.StatusCode);

        Assert.Equal(
            "application/problem+json",
            secondResponse.Content.Headers
                .ContentType?
                .MediaType);
    }
}