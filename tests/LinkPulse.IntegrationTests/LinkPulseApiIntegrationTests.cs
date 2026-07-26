using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LinkPulse.IntegrationTests;

public sealed class LinkPulseApiIntegrationTests
    : IClassFixture<LinkPulseWebApplicationFactory>
{
    private const string ValidPassword =
        "LinkPulse123";

    private readonly LinkPulseWebApplicationFactory factory;

    private readonly HttpClient client;

    public LinkPulseApiIntegrationTests(
        LinkPulseWebApplicationFactory factory)
    {
        this.factory = factory;

        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
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

        Assert.Equal(
            "Testing",
            payload.Environment);
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

    [Fact]
    public async Task RegisterAndLogin_ShouldReturnAccessTokens()
    {
        var registeredUser =
            await RegisterAsync(client);

        using var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email =
                        registeredUser.Auth.User.Email,

                    password =
                        registeredUser.Password
                });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login =
            await loginResponse.Content
                .ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(login);

        Assert.False(
            string.IsNullOrWhiteSpace(
                login.AccessToken));

        Assert.Equal(
            registeredUser.Auth.User.Id,
            login.User.Id);
    }

    [Fact]
    public async Task CreateLink_WithoutAlias_ShouldGenerateShortCode()
    {
        var registeredUser =
            await RegisterAsync(client);

        Authorize(
            client,
            registeredUser);

        var link = await CreateLinkAsync(
            client,
            "https://example.com/generated",
            customAlias: null);

        Assert.Equal(
            8,
            link.ShortCode.Length);

        Assert.Equal(
            link.ShortCode.ToLowerInvariant(),
            link.ShortCode);

        Assert.True(link.IsActive);
        Assert.False(link.IsExpired);
    }

    [Fact]
    public async Task CreateLink_WithDuplicateAlias_ShouldReturnConflict()
    {
        var registeredUser =
            await RegisterAsync(client);

        Authorize(
            client,
            registeredUser);

        var alias =
            CreateAlias("duplicate");

        _ = await CreateLinkAsync(
            client,
            "https://example.com/first",
            alias);

        using var conflictResponse =
            await client.PostAsJsonAsync(
                "/api/links",
                new
                {
                    targetUrl =
                        "https://example.com/second",

                    customAlias =
                        alias,

                    expiresAt =
                        (DateTimeOffset?)null
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            conflictResponse.StatusCode);
    }

    [Fact]
    public async Task CreateLink_WithInvalidUrl_ShouldReturnBadRequest()
    {
        var registeredUser =
            await RegisterAsync(client);

        Authorize(
            client,
            registeredUser);

        using var response =
            await client.PostAsJsonAsync(
                "/api/links",
                new
                {
                    targetUrl =
                        "example.com/no-scheme",

                    customAlias =
                        CreateAlias("invalid"),

                    expiresAt =
                        (DateTimeOffset?)null
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Redirect_ShouldUseCacheAndInvalidateAfterUpdate()
    {
        var registeredUser =
            await RegisterAsync(client);

        Authorize(
            client,
            registeredUser);

        var alias =
            CreateAlias("cache");

        const string originalTarget =
            "https://example.com/original";

        const string updatedTarget =
            "https://example.com/updated";

        var link = await CreateLinkAsync(
            client,
            originalTarget,
            alias);

        using var firstRedirect =
            await client.GetAsync(
                $"/{alias}");

        Assert.Equal(
            HttpStatusCode.Redirect,
            firstRedirect.StatusCode);

        Assert.Equal(
            "MISS",
            GetCacheStatus(firstRedirect));

        Assert.Equal(
            new Uri(originalTarget),
            firstRedirect.Headers.Location);

        using var secondRedirect =
            await client.GetAsync(
                $"/{alias}");

        Assert.Equal(
            HttpStatusCode.Redirect,
            secondRedirect.StatusCode);

        Assert.Equal(
            "HIT",
            GetCacheStatus(secondRedirect));

        using var updateResponse =
            await client.PutAsJsonAsync(
                $"/api/links/{link.Id}",
                new
                {
                    targetUrl =
                        updatedTarget,

                    expiresAt =
                        (DateTimeOffset?)null
                });

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        using var redirectAfterUpdate =
            await client.GetAsync(
                $"/{alias}");

        Assert.Equal(
            HttpStatusCode.Redirect,
            redirectAfterUpdate.StatusCode);

        Assert.Equal(
            "MISS",
            GetCacheStatus(
                redirectAfterUpdate));

        Assert.Equal(
            new Uri(updatedTarget),
            redirectAfterUpdate
                .Headers
                .Location);
    }

    [Fact]
    public async Task Redirect_UnknownCode_ShouldReturnNotFound()
    {
        var alias =
            CreateAlias("unknown");

        using var response =
            await client.GetAsync(
                $"/{alias}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Redirect_ExpiredLink_ShouldReturnGone()
    {
        var registeredUser =
            await RegisterAsync(client);

        var alias =
            CreateAlias("expired");

        var currentTime =
            DateTimeOffset.UtcNow;

        var expiredLink =
            new ShortLink(
                registeredUser.Auth.User.Id,
                alias,
                "https://example.com/expired",
                currentTime.AddMinutes(-2),
                currentTime.AddMinutes(-1));

        using (var scope =
               factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<
                        LinkPulseDbContext>();

            dbContext.ShortLinks.Add(
                expiredLink);

            await dbContext.SaveChangesAsync();
        }

        using var response =
            await client.GetAsync(
                $"/{alias}");

        Assert.Equal(
            HttpStatusCode.Gone,
            response.StatusCode);
    }

    [Fact]
    public async Task DifferentUser_ShouldNotUpdateForeignLink()
    {
        var owner =
            await RegisterAsync(client);

        Authorize(client, owner);

        var link = await CreateLinkAsync(
            client,
            "https://example.com/owner",
            CreateAlias("owner"));

        using var otherClient =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

        var otherUser =
            await RegisterAsync(otherClient);

        Authorize(
            otherClient,
            otherUser);

        using var response =
            await otherClient.PutAsJsonAsync(
                $"/api/links/{link.Id}",
                new
                {
                    targetUrl =
                        "https://example.com/foreign-update",

                    expiresAt =
                        (DateTimeOffset?)null
                });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Analytics_ShouldCountClicksAndPaginateEvents()
    {
        var registeredUser =
            await RegisterAsync(client);

        Authorize(
            client,
            registeredUser);

        var alias =
            CreateAlias("analytics");

        var link = await CreateLinkAsync(
            client,
            "https://example.com/analytics",
            alias);

        client.DefaultRequestHeaders.UserAgent
            .ParseAdd(
                "LinkPulse-IntegrationTests/1.0");

        client.DefaultRequestHeaders.Referrer =
            new Uri("https://github.com/");

        using (var firstRedirect =
               await client.GetAsync(
                   $"/{alias}"))
        {
            Assert.Equal(
                HttpStatusCode.Redirect,
                firstRedirect.StatusCode);
        }

        using (var secondRedirect =
               await client.GetAsync(
                   $"/{alias}"))
        {
            Assert.Equal(
                HttpStatusCode.Redirect,
                secondRedirect.StatusCode);
        }

        client.DefaultRequestHeaders.Referrer =
            null;

        using (var directRedirect =
               await client.GetAsync(
                   $"/{alias}"))
        {
            Assert.Equal(
                HttpStatusCode.Redirect,
                directRedirect.StatusCode);
        }

        using var analyticsResponse =
            await client.GetAsync(
                $"/api/links/{link.Id}/analytics");

        Assert.Equal(
            HttpStatusCode.OK,
            analyticsResponse.StatusCode);

        var analytics =
            await analyticsResponse.Content
                .ReadFromJsonAsync<
                    AnalyticsResponse>();

        Assert.NotNull(analytics);

        Assert.Equal(
            3L,
            analytics.TotalClicks);

        Assert.Equal(
            3L,
            analytics.TimeSeries.Sum(
                point => point.Clicks));

        Assert.Contains(
            analytics.TopReferrers,
            referrer =>
                referrer.Referrer
                    == "https://github.com/"
                && referrer.Clicks == 2);

        Assert.Contains(
            analytics.TopReferrers,
            referrer =>
                referrer.Referrer
                    == "(direct)"
                && referrer.Clicks == 1);

        using var eventsResponse =
            await client.GetAsync(
                $"/api/links/{link.Id}/events?page=1&pageSize=2");

        Assert.Equal(
            HttpStatusCode.OK,
            eventsResponse.StatusCode);

        var events =
            await eventsResponse.Content
                .ReadFromJsonAsync<
                    PagedEventsResponse>();

        Assert.NotNull(events);

        Assert.Equal(
            3L,
            events.TotalCount);

        Assert.Equal(
            2L,
            events.TotalPages);

        Assert.Equal(
            2,
            events.Items.Count);
    }

    private static async Task<RegisteredUser>
        RegisterAsync(
            HttpClient targetClient)
    {
        var email =
            $"user-{Guid.NewGuid():N}@example.com";

        using var response =
            await targetClient.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    email,

                    password =
                        ValidPassword
                });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var auth =
            await response.Content
                .ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);

        return new RegisteredUser(
            auth,
            ValidPassword);
    }

    private static void Authorize(
        HttpClient targetClient,
        RegisteredUser registeredUser)
    {
        targetClient.DefaultRequestHeaders
            .Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                registeredUser.Auth.AccessToken);
    }

    private static async Task<ShortLinkResponse>
        CreateLinkAsync(
            HttpClient targetClient,
            string targetUrl,
            string? customAlias)
    {
        using var response =
            await targetClient.PostAsJsonAsync(
                "/api/links",
                new
                {
                    targetUrl,

                    customAlias,

                    expiresAt =
                        (DateTimeOffset?)null
                });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var link =
            await response.Content
                .ReadFromJsonAsync<
                    ShortLinkResponse>();

        Assert.NotNull(link);

        return link;
    }

    private static string CreateAlias(
        string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            prefix);

        var normalizedPrefix =
            prefix.Trim().ToLowerInvariant();

        var suffix =
            Guid.NewGuid()
                .ToString("N")[..16];

        var alias =
            $"{normalizedPrefix}-{suffix}";

        Assert.True(
            alias.Length <= 32,
            $"Generated alias '{alias}' exceeds 32 characters.");

        return alias;
    }

    private static string GetCacheStatus(
        HttpResponseMessage response)
    {
        return Assert.Single(
            response.Headers.GetValues(
                "X-LinkPulse-Cache"));
    }

    private sealed record RegisteredUser(
        AuthResponse Auth,
        string Password);

    private sealed record AuthResponse(
        string AccessToken,
        UserResponse User);

    private sealed record UserResponse(
        Guid Id,
        string Email);

    private sealed record VersionResponse(
        string Name,
        string Version,
        string Environment);

    private sealed record ShortLinkResponse(
        Guid Id,
        string ShortCode,
        string TargetUrl,
        bool IsActive,
        bool IsExpired);

    private sealed record AnalyticsResponse(
        long TotalClicks,
        IReadOnlyList<AnalyticsPointResponse>
            TimeSeries,
        IReadOnlyList<ReferrerResponse>
            TopReferrers);

    private sealed record AnalyticsPointResponse(
        string Date,
        long Clicks);

    private sealed record ReferrerResponse(
        string Referrer,
        long Clicks);

    private sealed record PagedEventsResponse(
        long TotalCount,
        long TotalPages,
        IReadOnlyList<ClickEventResponse>
            Items);

    private sealed record ClickEventResponse(
        long Id,
        DateTimeOffset OccurredAt,
        string? Referrer,
        string? UserAgent);
}