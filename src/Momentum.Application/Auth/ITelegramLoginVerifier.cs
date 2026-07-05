namespace Momentum.Application.Auth;

/// <summary>
/// Verifies a Telegram login payload's HMAC signature and freshness.
/// Implemented in Infrastructure (needs the bot token). Throws
/// UnauthorizedException with code TELEGRAM_SIGNATURE_INVALID or
/// TELEGRAM_AUTH_EXPIRED on failure.
/// </summary>
public interface ITelegramLoginVerifier
{
    TelegramUserData Verify(TelegramLoginRequest request);
}
