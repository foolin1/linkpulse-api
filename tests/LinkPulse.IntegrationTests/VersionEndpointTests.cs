using System.Net;
using System.Net.Http.Json;

namespace LinkPulse.IntegrationTests;

public sealed class VersionEndpointTests
    : IClassFixture<LinkPulseWebApplicationFactory>
{
    private readonly HttpClient client;

    public VersionEndpointTests(
        LinkPulseWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVersion_ShouldReturnServiceMetadata()
    {
        using var response =
            await client.GetAsync("/version");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var payload =
            await response.Content
                .ReadFromJsonAsync<VersionResponse>();

        Assert.NotNull(payload);

        Assert.Equal(
            "LinkPulse.Api",
            payload.Name);

        Assert.False(
            string.IsNullOrWhiteSpace(
                payload.Version));

        Assert.False(
            string.IsNullOrWhiteSpace(
                payload.Environment));
    }

    [Fact]
    public async Task GetLinks_WithoutToken_ShouldReturnUnauthorized()
    {
        using var response =
            await client.GetAsync("/api/links");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private sealed record VersionResponse(
        string Name,
        string Version,
        string Environment);
}