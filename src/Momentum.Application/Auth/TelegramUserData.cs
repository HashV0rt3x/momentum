namespace Momentum.Application.Auth;

/// <summary>Signature-verified identity extracted from a Telegram login payload.</summary>
public sealed record TelegramUserData(
    long TelegramId,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    string? LanguageCode);
