using Momentum.Domain.Common;

namespace Momentum.Domain.Settings;

/// <summary>
/// Per-user preferences (spec section 2). Locale intentionally lives on the
/// user record, not here — the settings API surfaces it for convenience.
/// Created lazily with defaults on first GET /settings.
/// </summary>
public sealed class UserSettings : UserOwnedEntity
{
    private UserSettings()
    {
    }

    public UserSettings(Guid userId) : base(userId)
    {
    }

    /// <summary>light | dark | system.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>mon | sun.</summary>
    public string WeekStart { get; set; } = "mon";

    public int TimerFocusMinutes { get; set; } = 25;

    public int TimerShortBreakMinutes { get; set; } = 5;

    public int TimerLongBreakMinutes { get; set; } = 15;

    public int TimerRoundsBeforeLongBreak { get; set; } = 4;

    public bool NotifyTaskDue { get; set; } = true;

    public bool NotifyMentions { get; set; } = true;

    public bool NotifyWeeklySummary { get; set; } = true;

    public bool NotifyFocusReminders { get; set; }

    /// <summary>telegram | none.</summary>
    public string NotificationChannel { get; set; } = "telegram";
}
