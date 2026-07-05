namespace Momentum.Application.Auth;

/// <summary>Issues signed access tokens. Implemented in Infrastructure.</summary>
public interface IJwtTokenService
{
    /// <returns>The signed JWT and its lifetime in seconds (spec `expiresIn`).</returns>
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(Guid userId, string role, string name);
}
