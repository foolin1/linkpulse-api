using System.IdentityModel.Tokens.Jwt;
using System.Text;
using LinkPulse.Api.Authentication;
using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using LinkPulse.Api.Features.Auth;
using LinkPulse.Api.Features.Links;
using LinkPulse.Api.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var postgreSqlConnectionString =
    builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' is not configured.");

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' is not configured.");

var jwtSection = builder.Configuration.GetSection(
    JwtOptions.SectionName);

var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is not available.");

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
{
    throw new InvalidOperationException(
        "JWT issuer is not configured.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "JWT audience is not configured.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    || Encoding.UTF8.GetByteCount(
        jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

if (jwtOptions.AccessTokenLifetimeMinutes is < 1 or > 1440)
{
    throw new InvalidOperationException(
        "JWT access token lifetime must be between 1 and 1440 minutes.");
}

builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<LinkPulseDbContext>(
    options =>
    {
        options.UseNpgsql(postgreSqlConnectionString);
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ =>
    {
        var options =
            ConfigurationOptions.Parse(redisConnectionString);

        options.AbortOnConnectFail = false;

        return ConnectionMultiplexer.Connect(options);
    });

builder.Services.AddScoped<
    IPasswordHasher<ApplicationUser>,
    PasswordHasher<ApplicationUser>>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

builder.Services.AddSingleton<
    IShortCodeGenerator,
    ShortCodeGenerator>();

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(
        options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtOptions.SigningKey)),

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    NameClaimType =
                        JwtRegisteredClaimNames.Sub,

                    ClockSkew = TimeSpan.FromSeconds(30)
                };
        });

builder.Services.AddAuthorization();

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

app.UseAuthentication();
app.UseAuthorization();

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
                    version = assembly.Version?.ToString()
                        ?? "unknown",
                    environment = environment.EnvironmentName
                });
        })
    .WithName("GetVersion")
    .WithTags("System");

AuthEndpoints.Map(app);
LinkEndpoints.Map(app);

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