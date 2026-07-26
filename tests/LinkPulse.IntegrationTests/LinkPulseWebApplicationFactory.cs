using LinkPulse.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace LinkPulse.IntegrationTests;

public class LinkPulseWebApplicationFactory
    : WebApplicationFactory<Program>,
      IAsyncLifetime
{
    private const string TestSigningKey =
        "linkpulse-integration-tests-signing-key-2026-secure-value";

    private readonly PostgreSqlContainer postgreSqlContainer =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("linkpulse_tests")
            .WithUsername("linkpulse")
            .WithPassword("linkpulse_tests")
            .Build();

    private readonly RedisContainer redisContainer =
        new RedisBuilder("redis:7.4-alpine")
            .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            postgreSqlContainer.StartAsync(),
            redisContainer.StartAsync());
    }

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        var settings =
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] =
                    postgreSqlContainer
                        .GetConnectionString(),

                ["ConnectionStrings:Redis"] =
                    $"{redisContainer.GetConnectionString()},abortConnect=false",

                ["Jwt:SigningKey"] =
                    TestSigningKey,

                [HostDefaults.EnvironmentKey] =
                    "Testing",

                ["RateLimits:LinkCreationPermitLimit"] =
                    "1000",

                ["RateLimits:LinkCreationWindowSeconds"] =
                    "60",

                ["RateLimits:RedirectPermitLimit"] =
                    "1000",

                ["RateLimits:RedirectWindowSeconds"] =
                    "60",

                ["ExpirationCleanup:IntervalSeconds"] =
                    "3600",

                ["ExpirationCleanup:BatchSize"] =
                    "100"
            };

        CustomizeSettings(settings);

        builder.ConfigureHostConfiguration(
            configurationBuilder =>
            {
                configurationBuilder
                    .AddInMemoryCollection(settings);
            });

        ApplyMigrations();

        return base.CreateHost(builder);
    }

    protected virtual void CustomizeSettings(
        IDictionary<string, string?> settings)
    {
    }

    private void ApplyMigrations()
    {
        var options =
            new DbContextOptionsBuilder<
                    LinkPulseDbContext>()
                .UseNpgsql(
                    postgreSqlContainer
                        .GetConnectionString())
                .Options;

        using var dbContext =
            new LinkPulseDbContext(options);

        dbContext.Database.Migrate();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        await Task.WhenAll(
            postgreSqlContainer
                .DisposeAsync()
                .AsTask(),

            redisContainer
                .DisposeAsync()
                .AsTask());
    }
}