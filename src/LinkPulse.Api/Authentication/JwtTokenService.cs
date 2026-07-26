using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LinkPulse.Api.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LinkPulse.Api.Authentication;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtOptions jwtOptions = options.Value;

    public JwtTokenResult CreateToken(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = timeProvider.GetUtcNow();

        var expiresAt = issuedAt.AddMinutes(
            jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        var accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        var expiresInSeconds = checked(
            jwtOptions.AccessTokenLifetimeMinutes * 60);

        return new JwtTokenResult(
            accessToken,
            expiresAt,
            expiresInSeconds);
    }
}