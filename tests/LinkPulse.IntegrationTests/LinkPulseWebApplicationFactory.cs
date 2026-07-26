using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LinkPulse.IntegrationTests;

public sealed class LinkPulseWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestSigningKey =
        "linkpulse-integration-tests-signing-key-2026-secure-value";

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(
            configurationBuilder =>
            {
                configurationBuilder
                    .AddInMemoryCollection(
                        new Dictionary<
                            string,
                            string?>
                        {
                            ["Jwt:SigningKey"] =
                                TestSigningKey,

                            [HostDefaults
                                .EnvironmentKey] =
                                "Testing",

                            ["RateLimits:LinkCreationPermitLimit"] =
                                "1",

                            ["RateLimits:LinkCreationWindowSeconds"] =
                                "300",

                            ["RateLimits:RedirectPermitLimit"] =
                                "1",

                            ["RateLimits:RedirectWindowSeconds"] =
                                "300",

                            ["ExpirationCleanup:IntervalSeconds"] =
                                "3600",

                            ["ExpirationCleanup:BatchSize"] =
                                "10"
                        });
            });

        return base.CreateHost(builder);
    }
}