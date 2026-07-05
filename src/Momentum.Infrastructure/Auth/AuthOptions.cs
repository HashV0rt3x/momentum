namespace Momentum.Infrastructure.Auth;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public int AccessMinutes { get; set; } = 15;

    public int RefreshDays { get; set; } = 30;
}

/// <summary>Bound from the "Telegram" configuration section.</summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>
    /// Bot token from @BotFather. Required for /auth/telegram — validated at
    /// first use (not startup) so the app can boot without it in environments
    /// that never exercise auth (e.g. health-check-only smoke tests).
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    public string? BotUsername { get; set; }

    /// <summary>
    /// Long-poll the bot for /start messages and hand out 6-digit login codes.
    /// Off in tests; a no-op anyway when BotToken is empty.
    /// </summary>
    public bool EnablePolling { get; set; } = true;

    /// <summary>Login code lifetime in minutes.</summary>
    public int LoginCodeMinutes { get; set; } = 5;
}
