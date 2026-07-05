using Microsoft.EntityFrameworkCore;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Settings;
using Momentum.Domain.Settings;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Settings;

public sealed class SettingsService(AppDbContext dbContext) : ISettingsService
{
    public async Task<SettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (settings, locale) = await GetOrCreateAsync(userId, cancellationToken);
        return ToDto(settings, locale);
    }

    public async Task<SettingsDto> UpdateAsync(Guid userId, UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        var (settings, locale) = await GetOrCreateAsync(userId, cancellationToken);

        if (request.Theme is not null)
        {
            settings.Theme = request.Theme;
        }

        if (request.WeekStart is not null)
        {
            settings.WeekStart = request.WeekStart;
        }

        if (request.Timer is not null)
        {
            settings.TimerFocusMinutes = request.Timer.Focus ?? settings.TimerFocusMinutes;
            settings.TimerShortBreakMinutes = request.Timer.ShortBreak ?? settings.TimerShortBreakMinutes;
            settings.TimerLongBreakMinutes = request.Timer.LongBreak ?? settings.TimerLongBreakMinutes;
            settings.TimerRoundsBeforeLongBreak = request.Timer.RoundsBeforeLongBreak ?? settings.TimerRoundsBeforeLongBreak;
        }

        if (request.Notifications is not null)
        {
            settings.NotifyTaskDue = request.Notifications.TaskDue ?? settings.NotifyTaskDue;
            settings.NotifyMentions = request.Notifications.Mentions ?? settings.NotifyMentions;
            settings.NotifyWeeklySummary = request.Notifications.WeeklySummary ?? settings.NotifyWeeklySummary;
            settings.NotifyFocusReminders = request.Notifications.FocusReminders ?? settings.NotifyFocusReminders;
            settings.NotificationChannel = request.Notifications.Channel ?? settings.NotificationChannel;
        }

        // Locale lives on the user record (it drives i18n everywhere), the
        // settings API just proxies it.
        if (request.Locale is not null && request.Locale != locale)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new NotFoundException("User", userId);
            user.Locale = request.Locale;
            locale = request.Locale;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(settings, locale);
    }

    private async Task<(UserSettings Settings, string Locale)> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var locale = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Locale)
            .FirstOrDefaultAsync(cancellationToken) ?? "uz";

        if (settings is null)
        {
            settings = new UserSettings(userId);
            dbContext.UserSettings.Add(settings);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (settings, locale);
    }

    private static SettingsDto ToDto(UserSettings s, string locale) => new(
        Locale: locale,
        Theme: s.Theme,
        WeekStart: s.WeekStart,
        Timer: new TimerSettingsDto(
            s.TimerFocusMinutes,
            s.TimerShortBreakMinutes,
            s.TimerLongBreakMinutes,
            s.TimerRoundsBeforeLongBreak),
        Notifications: new NotificationSettingsDto(
            s.NotifyTaskDue,
            s.NotifyMentions,
            s.NotifyWeeklySummary,
            s.NotifyFocusReminders,
            s.NotificationChannel));
}
