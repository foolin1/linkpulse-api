using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkPulse.IntegrationTests;

public sealed class VersionEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public VersionEndpointTests(
        WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVersion_ShouldReturnServiceMetadata()
    {
        using var response = await client.GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload =
            await response.Content.ReadFromJsonAsync<VersionResponse>();

        Assert.NotNull(payload);
        Assert.Equal("LinkPulse.Api", payload.Name);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
        Assert.False(string.IsNullOrWhiteSpace(payload.Environment));
    }

    private sealed record VersionResponse(
        string Name,
        string Version,
        string Environment);
}