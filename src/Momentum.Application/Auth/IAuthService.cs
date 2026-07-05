namespace Momentum.Application.Auth;

public interface IAuthService
{
    /// <summary>
    /// Sign in / sign up with a verified Telegram payload. On first login the
    /// user's locale is chosen from Telegram language_code (if uz/ru/en), then
    /// <paramref name="preferredLocale"/> (from Accept-Language), then "uz".
    /// </summary>
    Task<AuthResponse> LoginWithTelegramAsync(
        TelegramLoginRequest request,
        string? preferredLocale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sign in / sign up with a 6-digit code the Telegram bot handed out in
    /// response to /start. Single-use, short-lived.
    /// </summary>
    Task<AuthResponse> LoginWithTelegramCodeAsync(
        string code,
        string? preferredLocale,
        CancellationToken cancellationToken);

    /// <summary>Rotate a refresh token: the old one is revoked, a new pair is issued.</summary>
    Task<TokenPairResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revoke the given refresh token for the given user. Idempotent.</summary>
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
}
