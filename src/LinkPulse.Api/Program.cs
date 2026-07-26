using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using LinkPulse.Api.Authentication;
using LinkPulse.Api.Caching;
using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using LinkPulse.Api.Expiration;
using LinkPulse.Api.Features.Analytics;
using LinkPulse.Api.Features.Auth;
using LinkPulse.Api.Features.Links;
using LinkPulse.Api.Features.Redirects;
using LinkPulse.Api.HealthChecks;
using LinkPulse.Api.RateLimiting;
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
    builder.Configuration.GetConnectionString(
        "PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' is not configured.");

var redisConnectionString =
    builder.Configuration.GetConnectionString(
        "Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' is not configured.");

var jwtSection =
    builder.Configuration.GetSection(
        JwtOptions.SectionName);

var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is not available.");

if (string.IsNullOrWhiteSpace(
        jwtOptions.Issuer))
{
    throw new InvalidOperationException(
        "JWT issuer is not configured.");
}

if (string.IsNullOrWhiteSpace(
        jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "JWT audience is not configured.");
}

if (string.IsNullOrWhiteSpace(
        jwtOptions.SigningKey)
    || Encoding.UTF8.GetByteCount(
        jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

if (jwtOptions.AccessTokenLifetimeMinutes
    is < 1 or > 1440)
{
    throw new InvalidOperationException(
        "JWT access token lifetime must be between 1 and 1440 minutes.");
}

var linkCacheSection =
    builder.Configuration.GetSection(
        LinkCacheOptions.SectionName);

var linkCacheOptions =
    linkCacheSection.Get<LinkCacheOptions>()
    ?? new LinkCacheOptions();

if (string.IsNullOrWhiteSpace(
        linkCacheOptions.KeyPrefix))
{
    throw new InvalidOperationException(
        "Redis link cache key prefix is not configured.");
}

if (linkCacheOptions.DefaultTtlMinutes
    is < 1 or > 10080)
{
    throw new InvalidOperationException(
        "Redis link cache TTL must be between 1 and 10080 minutes.");
}

var rateLimitSection =
    builder.Configuration.GetSection(
        LinkPulseRateLimitOptions.SectionName);

var rateLimitOptions =
    rateLimitSection
        .Get<LinkPulseRateLimitOptions>()
    ?? new LinkPulseRateLimitOptions();

if (rateLimitOptions
        .LinkCreationPermitLimit
    is < 1 or > 100000)
{
    throw new InvalidOperationException(
        "Link creation rate limit must be between 1 and 100000 requests.");
}

if (rateLimitOptions
        .LinkCreationWindowSeconds
    is < 1 or > 86400)
{
    throw new InvalidOperationException(
        "Link creation rate limit window must be between 1 and 86400 seconds.");
}

if (rateLimitOptions.RedirectPermitLimit
    is < 1 or > 100000)
{
    throw new InvalidOperationException(
        "Redirect rate limit must be between 1 and 100000 requests.");
}

if (rateLimitOptions.RedirectWindowSeconds
    is < 1 or > 86400)
{
    throw new InvalidOperationException(
        "Redirect rate limit window must be between 1 and 86400 seconds.");
}

var expirationCleanupSection =
    builder.Configuration.GetSection(
        ExpiredLinkCleanupOptions.SectionName);

var expirationCleanupOptions =
    expirationCleanupSection
        .Get<ExpiredLinkCleanupOptions>()
    ?? new ExpiredLinkCleanupOptions();

if (expirationCleanupOptions.IntervalSeconds
    is < 1 or > 86400)
{
    throw new InvalidOperationException(
        "Expiration cleanup interval must be between 1 and 86400 seconds.");
}

if (expirationCleanupOptions.BatchSize
    is < 1 or > 1000)
{
    throw new InvalidOperationException(
        "Expiration cleanup batch size must be between 1 and 1000.");
}

builder.Services.Configure<JwtOptions>(
    jwtSection);

builder.Services.Configure<LinkCacheOptions>(
    linkCacheSection);

builder.Services.Configure<
    LinkPulseRateLimitOptions>(
    rateLimitSection);

builder.Services.Configure<
    ExpiredLinkCleanupOptions>(
    expirationCleanupSection);

builder.Services.AddSingleton(
    TimeProvider.System);

builder.Services.AddDbContext<
    LinkPulseDbContext>(
    options =>
    {
        options.UseNpgsql(
            postgreSqlConnectionString);
    });

builder.Services.AddSingleton<
    IConnectionMultiplexer>(
    _ =>
    {
        var options =
            ConfigurationOptions.Parse(
                redisConnectionString);

        options.AbortOnConnectFail = false;

        return ConnectionMultiplexer.Connect(
            options);
    });

builder.Services.AddSingleton<
    ILinkCache,
    RedisLinkCache>();

builder.Services.AddScoped<
    IClickEventRecorder,
    ClickEventRecorder>();

builder.Services.AddScoped<
    IExpiredLinkProcessor,
    ExpiredLinkProcessor>();

builder.Services.AddHostedService<
    ExpiredLinkCleanupWorker>();

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
        JwtBearerDefaults
            .AuthenticationScheme)
    .AddJwtBearer(
        options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,

                    ValidIssuer =
                        jwtOptions.Issuer,

                    ValidateAudience = true,

                    ValidAudience =
                        jwtOptions.Audience,

                    ValidateIssuerSigningKey =
                        true,

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtOptions
                                    .SigningKey)),

                    ValidateLifetime = true,

                    RequireExpirationTime =
                        true,

                    NameClaimType =
                        JwtRegisteredClaimNames
                            .Sub,

                    ClockSkew =
                        TimeSpan.FromSeconds(30)
                };
        });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode =
            StatusCodes
                .Status429TooManyRequests;

        options.OnRejected =
            async (
                context,
                cancellationToken) =>
            {
                var response =
                    context.HttpContext.Response;

                response.StatusCode =
                    StatusCodes
                        .Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter,
                        out var retryAfter))
                {
                    var retryAfterSeconds =
                        Math.Max(
                            1,
                            (int)Math.Ceiling(
                                retryAfter
                                    .TotalSeconds));

                    response.Headers[
                        "Retry-After"] =
                        retryAfterSeconds.ToString(
                            CultureInfo
                                .InvariantCulture);
                }

                await Results.Problem(
                        statusCode:
                            StatusCodes
                                .Status429TooManyRequests,
                        title:
                            "Request rate limit exceeded",
                        detail:
                            "Too many requests were sent in the current time window.")
                    .ExecuteAsync(
                        context.HttpContext);
            };

        options.AddPolicy(
            RateLimitPolicyNames.LinkCreation,
            httpContext =>
                RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            RateLimitPartitionKeyProvider
                                .ForLinkCreation(
                                    httpContext),
                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        rateLimitOptions
                                            .LinkCreationPermitLimit,

                                    Window =
                                        TimeSpan.FromSeconds(
                                            rateLimitOptions
                                                .LinkCreationWindowSeconds),

                                    QueueProcessingOrder =
                                        QueueProcessingOrder
                                            .OldestFirst,

                                    QueueLimit = 0,

                                    AutoReplenishment =
                                        true
                                }));

        options.AddPolicy(
            RateLimitPolicyNames.Redirects,
            httpContext =>
                RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            RateLimitPartitionKeyProvider
                                .ForRedirect(
                                    httpContext),
                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        rateLimitOptions
                                            .RedirectPermitLimit,

                                    Window =
                                        TimeSpan.FromSeconds(
                                            rateLimitOptions
                                                .RedirectWindowSeconds),

                                    QueueProcessingOrder =
                                        QueueProcessingOrder
                                            .OldestFirst,

                                    QueueLimit = 0,

                                    AutoReplenishment =
                                        true
                                }));
    });

builder.Services
    .AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>(
        "postgresql",
        failureStatus:
            HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus:
            HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

app.UseRouting();

app.UseAuthentication();

app.UseRateLimiter();

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
                service =
                    "LinkPulse API",

                status =
                    "Running",

                documentation =
                    "/openapi/v1.json"
            }))
    .WithName("GetServiceInfo")
    .WithTags("System");

app.MapGet(
        "/version",
        (IHostEnvironment environment) =>
        {
            var assembly =
                typeof(Program)
                    .Assembly
                    .GetName();

            return Results.Ok(
                new
                {
                    name =
                        assembly.Name,

                    version =
                        assembly.Version
                            ?.ToString()
                        ?? "unknown",

                    environment =
                        environment
                            .EnvironmentName
                });
        })
    .WithName("GetVersion")
    .WithTags("System");

AuthEndpoints.Map(app);
LinkEndpoints.Map(app);
AnalyticsEndpoints.Map(app);
RedirectEndpoints.Map(app);

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,

        ResponseWriter =
            HealthCheckResponseWriter
                .WriteAsync
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = healthCheck =>
            healthCheck.Tags.Contains(
                "ready"),

        ResponseWriter =
            HealthCheckResponseWriter
                .WriteAsync
    });

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = healthCheck =>
            healthCheck.Tags.Contains(
                "ready"),

        ResponseWriter =
            HealthCheckResponseWriter
                .WriteAsync
    });

app.Run();

public partial class Program;