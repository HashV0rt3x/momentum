namespace Momentum.Domain.Tasks;

/// <summary>Wire format: todo | in_progress | done (snake_case).</summary>
public enum TaskItemStatus
{
    Todo,
    InProgress,
    Done,
}

/// <summary>Wire format: low | medium | high | urgent.</summary>
public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent,
}
