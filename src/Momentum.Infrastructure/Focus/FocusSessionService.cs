using Microsoft.EntityFrameworkCore;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Focus;
using Momentum.Domain.Focus;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Focus;

public sealed class FocusSessionService(AppDbContext dbContext, TimeProvider timeProvider) : IFocusSessionService
{
    public async Task<IReadOnlyList<FocusSessionDto>> ListAsync(
        Guid userId,
        FocusSessionListQuery query,
        CancellationToken cancellationToken)
    {
        var (from, to) = await ResolveRangeAsync(userId, query, cancellationToken);

        return await dbContext.FocusSessions
            .Where(s => s.UserId == userId && s.StartedAt >= from && s.StartedAt < to)
            .OrderBy(s => s.StartedAt)
            .Select(s => new FocusSessionDto(
                s.Id.ToString(),
                s.Type == FocusSessionType.Focus ? "focus"
                    : s.Type == FocusSessionType.ShortBreak ? "short_break" : "long_break",
                s.TaskId == null ? null : s.TaskId.ToString(),
                s.PlannedMinutes,
                s.CompletedMinutes,
                s.Completed,
                s.StartedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FocusSessionDto> CreateAsync(
        Guid userId,
        CreateFocusSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!FocusEnumMapping.TryParse(request.Type, out var type))
        {
            type = FocusSessionType.Focus; // unreachable — validator gates this
        }

        if (request.TaskId is not null)
        {
            var taskExists = await dbContext.Tasks
                .AnyAsync(t => t.UserId == userId && t.Id == request.TaskId, cancellationToken);

            if (!taskExists)
            {
                throw new NotFoundException("Task", request.TaskId);
            }
        }

        var session = new FocusSession(
            userId,
            type,
            request.TaskId,
            request.PlannedMinutes,
            request.CompletedMinutes,
            request.Completed,
            request.StartedAt);

        dbContext.FocusSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FocusSessionDto(
            session.Id.ToString(),
            session.Type.ToWire(),
            session.TaskId?.ToString(),
            session.PlannedMinutes,
            session.CompletedMinutes,
            session.Completed,
            session.StartedAt);
    }

    /// <summary>
    /// date=today is resolved as the calendar day in the USER'S timezone, not
    /// UTC — otherwise late-evening sessions in Tashkent would land on the wrong
    /// day. Explicit from/to wins over date. No params = today.
    /// </summary>
    private async Task<(DateTimeOffset From, DateTimeOffset To)> ResolveRangeAsync(
        Guid userId,
        FocusSessionListQuery query,
        CancellationToken cancellationToken)
    {
        if (query.From is not null || query.To is not null)
        {
            return (query.From ?? DateTimeOffset.MinValue, query.To ?? DateTimeOffset.MaxValue);
        }

        var timeZoneId = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken) ?? "UTC";

        var timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz)
            ? tz
            : TimeZoneInfo.Utc;

        var nowLocal = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var startOfDayLocal = new DateTimeOffset(nowLocal.Date, nowLocal.Offset);

        return (startOfDayLocal.ToUniversalTime(), startOfDayLocal.AddDays(1).ToUniversalTime());
    }
}
