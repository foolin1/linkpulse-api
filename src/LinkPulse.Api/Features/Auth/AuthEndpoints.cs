using System.Security.Claims;
using LinkPulse.Api.Authentication;
using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinkPulse.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost(
                "/register",
                RegisterAsync)
            .AllowAnonymous()
            .WithName("Register");

        group.MapPost(
                "/login",
                LoginAsync)
            .AllowAnonymous()
            .WithName("Login");

        group.MapGet(
                "/me",
                GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        LinkPulseDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IJwtTokenService jwtTokenService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validationErrors =
            AuthInputValidator.ValidateRegister(request);

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                validationErrors);
        }

        var email = request.Email!.Trim();

        var normalizedEmail =
            AuthInputValidator.NormalizeEmail(email);

        var emailAlreadyExists =
            await dbContext.ApplicationUsers
                .AsNoTracking()
                .AnyAsync(
                    user =>
                        user.NormalizedEmail
                        == normalizedEmail,
                    cancellationToken);

        if (emailAlreadyExists)
        {
            return EmailConflict();
        }

        var user = new ApplicationUser(
            email,
            normalizedEmail,
            timeProvider.GetUtcNow());

        var passwordHash = passwordHasher.HashPassword(
            user,
            request.Password!);

        user.SetPasswordHash(passwordHash);

        dbContext.ApplicationUsers.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            return EmailConflict();
        }

        var token = jwtTokenService.CreateToken(user);

        return Results.Created(
            "/api/auth/me",
            CreateAuthResponse(user, token));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LinkPulseDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IJwtTokenService jwtTokenService,
        CancellationToken cancellationToken)
    {
        var validationErrors =
            AuthInputValidator.ValidateLogin(request);

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                validationErrors);
        }

        var normalizedEmail =
            AuthInputValidator.NormalizeEmail(
                request.Email!);

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                applicationUser =>
                    applicationUser.NormalizedEmail
                    == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            return AuthenticationFailed();
        }

        var verificationResult =
            passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password!);

        if (verificationResult
            == PasswordVerificationResult.Failed)
        {
            return AuthenticationFailed();
        }

        if (verificationResult
            == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var updatedHash = passwordHasher.HashPassword(
                user,
                request.Password!);

            user.SetPasswordHash(updatedHash);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        var token = jwtTokenService.CreateToken(user);

        return Results.Ok(
            CreateAuthResponse(user, token));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                applicationUser =>
                    applicationUser.Id == userId.Value,
                cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            new UserResponse(
                user.Id,
                user.Email,
                user.CreatedAt));
    }

    private static AuthResponse CreateAuthResponse(
        ApplicationUser user,
        JwtTokenResult token)
    {
        return new AuthResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresInSeconds,
            token.ExpiresAt,
            new UserResponse(
                user.Id,
                user.Email,
                user.CreatedAt));
    }

    private static IResult EmailConflict()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Email is already registered",
            detail:
                "A user with the specified email already exists.");
    }

    private static IResult AuthenticationFailed()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication failed",
            detail: "Email or password is incorrect.");
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
        {
            SqlState:
                    PostgresErrorCodes.UniqueViolation
        };
    }
}