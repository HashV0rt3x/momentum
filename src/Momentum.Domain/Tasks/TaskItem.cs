using Momentum.Domain.Common;

namespace Momentum.Domain.Tasks;

/// <summary>
/// Spec section 6. Named TaskItem to avoid colliding with System.Threading.Tasks.Task.
/// TrackedMinutes is NOT stored here — it is computed from time entries at read
/// time (0 until the time-tracking module lands).
/// </summary>
public sealed class TaskItem : UserOwnedEntity
{
    private TaskItem()
    {
    }

    public TaskItem(Guid userId, string title) : base(userId)
    {
        Title = title;
    }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public Guid? ProjectId { get; set; }

    public string? CategoryKey { get; set; }

    public List<Guid> AssigneeIds { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public List<Subtask> Subtasks { get; set; } = [];

    public int? EstimatedMinutes { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset? StartDate { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>Child row of TaskItem; user isolation is enforced through the parent.</summary>
public sealed class Subtask : Entity
{
    private Subtask()
    {
    }

    public Subtask(Guid taskId, string title)
    {
        TaskId = taskId;
        Title = title;
    }

    public Guid TaskId { get; private set; }

    public string Title { get; set; } = string.Empty;

    public bool Done { get; set; }
}
