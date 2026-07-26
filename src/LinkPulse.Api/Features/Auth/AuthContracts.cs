namespace LinkPulse.Api.Features.Auth;

public sealed record RegisterRequest(
    string? Email,
    string? Password);

public sealed record LoginRequest(
    string? Email,
    string? Password);

public sealed record UserResponse(
    Guid Id,
    string Email,
    DateTimeOffset CreatedAt);

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    UserResponse User);