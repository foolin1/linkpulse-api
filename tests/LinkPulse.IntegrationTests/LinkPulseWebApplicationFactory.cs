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
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:SigningKey"] =
                            TestSigningKey,

                        [HostDefaults.EnvironmentKey] =
                            "Testing"
                    });
            });

        return base.CreateHost(builder);
    }
}