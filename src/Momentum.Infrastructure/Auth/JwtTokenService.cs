using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Momentum.Application.Auth;

namespace Momentum.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(Guid userId, string role, string name)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("name", name),
            new(ClaimTypes.Role, role),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), jwt.AccessMinutes * 60);
    }
}
