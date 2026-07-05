using Momentum.Domain.Common;

namespace Momentum.Domain.Focus;

/// <summary>Wire format: focus | short_break | long_break.</summary>
public enum FocusSessionType
{
    Focus,
    ShortBreak,
    LongBreak,
}

/// <summary>
/// Spec section 8 — a finished or aborted Pomodoro round, recorded by the client
/// when the timer stops (feeds "sessions today" + streaks).
/// </summary>
public sealed class FocusSession : UserOwnedEntity
{
    private FocusSession()
    {
    }

    public FocusSession(
        Guid userId,
        FocusSessionType type,
        Guid? taskId,
        int plannedMinutes,
        int completedMinutes,
        bool completed,
        DateTimeOffset startedAt) : base(userId)
    {
        Type = type;
        TaskId = taskId;
        PlannedMinutes = plannedMinutes;
        CompletedMinutes = completedMinutes;
        Completed = completed;
        StartedAt = startedAt;
    }

    public FocusSessionType Type { get; private set; }

    public Guid? TaskId { get; private set; }

    public int PlannedMinutes { get; private set; }

    public int CompletedMinutes { get; private set; }

    /// <summary>True when the round ran to the end (not aborted early).</summary>
    public bool Completed { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
}
