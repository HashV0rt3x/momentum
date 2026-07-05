using Momentum.Domain.Common;

namespace Momentum.Domain.Auth;

/// <summary>
/// One-time 6-digit login code issued by the Telegram bot when a user sends
/// /start. The user types the code into the web app, which exchanges it for a
/// token pair via POST /auth/telegram/code. Short-lived and single-use.
/// Not IUserOwned: it exists before the user is authenticated (or even created).
/// </summary>
public sealed class TelegramLoginCode : Entity
{
    private TelegramLoginCode()
    {
    }

    public TelegramLoginCode(
        string code,
        long telegramId,
        string firstName,
        string? lastName,
        string? username,
        string? languageCode,
        DateTimeOffset expiresAtUtc)
    {
        Code = code;
        TelegramId = telegramId;
        FirstName = firstName;
        LastName = lastName;
        Username = username;
        LanguageCode = languageCode;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string Code { get; private set; } = string.Empty;

    public long TelegramId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string? LastName { get; private set; }

    public string? Username { get; private set; }

    public string? LanguageCode { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    public bool IsActive(DateTimeOffset now) => UsedAtUtc is null && now < ExpiresAtUtc;

    public void MarkUsed(DateTimeOffset now) => UsedAtUtc ??= now;
}
