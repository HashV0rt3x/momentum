using FluentValidation;

namespace Momentum.Application.Settings;

public sealed record TimerSettingsDto(int Focus, int ShortBreak, int LongBreak, int RoundsBeforeLongBreak);

public sealed record NotificationSettingsDto(
    bool TaskDue,
    bool Mentions,
    bool WeeklySummary,
    bool FocusReminders,
    string Channel);

/// <summary>GET /settings response (spec section 2).</summary>
public sealed record SettingsDto(
    string Locale,
    string Theme,
    string WeekStart,
    TimerSettingsDto Timer,
    NotificationSettingsDto Notifications);

public sealed record UpdateTimerRequest(int? Focus, int? ShortBreak, int? LongBreak, int? RoundsBeforeLongBreak);

public sealed record UpdateNotificationsRequest(
    bool? TaskDue,
    bool? Mentions,
    bool? WeeklySummary,
    bool? FocusReminders,
    string? Channel);

/// <summary>PATCH /settings — any subset; nulls mean "leave unchanged".</summary>
public sealed record UpdateSettingsRequest(
    string? Locale,
    string? Theme,
    string? WeekStart,
    UpdateTimerRequest? Timer,
    UpdateNotificationsRequest? Notifications);

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Locale)
            .Must(l => l is "uz" or "ru" or "en")
            .WithMessage("Locale must be one of: uz, ru, en.")
            .When(x => x.Locale is not null);

        RuleFor(x => x.Theme)
            .Must(t => t is "light" or "dark" or "system")
            .WithMessage("Theme must be one of: light, dark, system.")
            .When(x => x.Theme is not null);

        RuleFor(x => x.WeekStart)
            .Must(w => w is "mon" or "sun")
            .WithMessage("WeekStart must be 'mon' or 'sun'.")
            .When(x => x.WeekStart is not null);

        When(x => x.Timer is not null, () =>
        {
            RuleFor(x => x.Timer!.Focus).InclusiveBetween(1, 240).When(x => x.Timer!.Focus is not null);
            RuleFor(x => x.Timer!.ShortBreak).InclusiveBetween(1, 60).When(x => x.Timer!.ShortBreak is not null);
            RuleFor(x => x.Timer!.LongBreak).InclusiveBetween(1, 120).When(x => x.Timer!.LongBreak is not null);
            RuleFor(x => x.Timer!.RoundsBeforeLongBreak).InclusiveBetween(1, 12).When(x => x.Timer!.RoundsBeforeLongBreak is not null);
        });

        RuleFor(x => x.Notifications!.Channel)
            .Must(c => c is "telegram" or "none")
            .WithMessage("Notification channel must be 'telegram' or 'none'.")
            .When(x => x.Notifications?.Channel is not null);
    }
}

public interface ISettingsService
{
    Task<SettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<SettingsDto> UpdateAsync(Guid userId, UpdateSettingsRequest request, CancellationToken cancellationToken);
}
