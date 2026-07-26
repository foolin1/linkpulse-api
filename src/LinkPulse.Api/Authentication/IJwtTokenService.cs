using LinkPulse.Api.Data.Entities;

namespace LinkPulse.Api.Authentication;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(ApplicationUser user);
}

public sealed record JwtTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);