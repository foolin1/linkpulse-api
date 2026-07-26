using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LinkPulse.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subjectClaim = principal.FindFirst(
            JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subjectClaim?.Value, out var userId)
            ? userId
            : null;
    }
}