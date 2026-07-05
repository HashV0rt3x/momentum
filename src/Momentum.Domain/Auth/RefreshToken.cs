using Momentum.Domain.Common;

namespace Momentum.Domain.Auth;

/// <summary>
/// A refresh token issued to a user. Only the SHA-256 hash of the token is
/// stored — the raw value exists solely in the response to the client.
/// Deliberately NOT <see cref="IUserOwned"/>: the refresh flow runs
/// unauthenticated (the token itself is the credential), so it must not be
/// subject to the per-user global query filter.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the raw token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && now < ExpiresAtUtc;

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;
}
