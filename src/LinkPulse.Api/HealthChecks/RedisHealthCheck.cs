using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace LinkPulse.Api.HealthChecks;

public sealed class RedisHealthCheck(
    IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = connectionMultiplexer.GetDatabase();

            var latency = await database
                .PingAsync()
                .WaitAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"Redis responded in {latency.TotalMilliseconds:F0} ms.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Redis connection is unavailable.",
                exception);
        }
    }
}