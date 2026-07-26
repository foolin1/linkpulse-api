using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LinkPulse.Api.HealthChecks;

public sealed class PostgreSqlHealthCheck(
    IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            configuration.GetConnectionString("PostgreSql");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection string is not configured.");
        }

        try
        {
            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new NpgsqlCommand("SELECT 1;", connection);

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                "PostgreSQL connection is available.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection is unavailable.",
                exception);
        }
    }
}