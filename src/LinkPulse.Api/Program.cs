using LinkPulse.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' is not configured.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(options);
});

builder.Services
    .AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet(
        "/",
        () => Results.Ok(
            new
            {
                service = "LinkPulse API",
                status = "Running",
                documentation = "/openapi/v1.json"
            }))
    .WithName("GetServiceInfo")
    .WithTags("System");

app.MapGet(
        "/version",
        (IHostEnvironment environment) =>
        {
            var assembly = typeof(Program).Assembly.GetName();

            return Results.Ok(
                new
                {
                    name = assembly.Name,
                    version = assembly.Version?.ToString() ?? "unknown",
                    environment = environment.EnvironmentName
                });
        })
    .WithName("GetVersion")
    .WithTags("System");

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = healthCheck =>
            healthCheck.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = healthCheck =>
            healthCheck.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

app.Run();

public partial class Program;
