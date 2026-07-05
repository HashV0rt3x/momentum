using FluentValidation;
using Momentum.Application.Common;
using Momentum.Domain.Tasks;

namespace Momentum.Application.Tasks;

public sealed record SubtaskDto(string Id, string Title, bool Done);

/// <summary>Spec section 6.</summary>
public sealed record TaskDto(
    string Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    string? ProjectId,
    string? CategoryKey,
    IReadOnlyList<string> AssigneeIds,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SubtaskDto> Subtasks,
    int? EstimatedMinutes,
    int TrackedMinutes,
    DateTimeOffset? DueDate,
    DateTimeOffset? StartDate,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    string? Status,
    string? Priority,
    string? CategoryKey,
    Guid? ProjectId,
    IReadOnlyList<Guid>? AssigneeIds,
    IReadOnlyList<string>? Tags,
    int? EstimatedMinutes,
    DateTimeOffset? DueDate,
    DateTimeOffset? StartDate);

/// <summary>PATCH — nulls mean "leave unchanged". status:"done" sets completedAt server-side.</summary>
public sealed record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    string? CategoryKey,
    Guid? ProjectId,
    IReadOnlyList<Guid>? AssigneeIds,
    IReadOnlyList<string>? Tags,
    int? EstimatedMinutes,
    DateTimeOffset? DueDate,
    DateTimeOffset? StartDate);

public sealed record CreateSubtaskRequest(string Title);

public sealed record UpdateSubtaskRequest(string? Title, bool? Done);

/// <summary>GET /tasks query (all filters optional).</summary>
public sealed record TaskListQuery(
    string? Status,
    string? Priority,
    Guid? ProjectId,
    Guid? AssigneeId,
    string? CategoryKey,
    DateTimeOffset? DueBefore,
    string? Q,
    int? Page,
    int? PageSize);

/// <summary>Wire-format mapping: todo | in_progress | done, low | medium | high | urgent.</summary>
public static class TaskEnumMapping
{
    public static string ToWire(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Todo => "todo",
        TaskItemStatus.InProgress => "in_progress",
        TaskItemStatus.Done => "done",
        _ => "todo",
    };

    public static string ToWire(this TaskPriority priority) => priority.ToString().ToLowerInvariant();

    public static bool TryParseStatus(string value, out TaskItemStatus status)
    {
        status = value switch
        {
            "todo" => TaskItemStatus.Todo,
            "in_progress" => TaskItemStatus.InProgress,
            "done" => TaskItemStatus.Done,
            _ => (TaskItemStatus)(-1),
        };
        return (int)status >= 0;
    }

    public static bool TryParsePriority(string value, out TaskPriority priority)
        => Enum.TryParse(value, ignoreCase: true, out priority) && Enum.IsDefined(priority);
}

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.Status)
            .Must(s => TaskEnumMapping.TryParseStatus(s!, out _))
            .WithMessage("Status must be one of: todo, in_progress, done.")
            .When(x => x.Status is not null);
        RuleFor(x => x.Priority)
            .Must(p => TaskEnumMapping.TryParsePriority(p!, out _))
            .WithMessage("Priority must be one of: low, medium, high, urgent.")
            .When(x => x.Priority is not null);
        RuleFor(x => x.CategoryKey).Matches("^[a-z0-9_]{1,64}$").When(x => x.CategoryKey is not null);
        RuleFor(x => x.EstimatedMinutes).GreaterThan(0).When(x => x.EstimatedMinutes is not null);
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.Status)
            .Must(s => TaskEnumMapping.TryParseStatus(s!, out _))
            .WithMessage("Status must be one of: todo, in_progress, done.")
            .When(x => x.Status is not null);
        RuleFor(x => x.Priority)
            .Must(p => TaskEnumMapping.TryParsePriority(p!, out _))
            .WithMessage("Priority must be one of: low, medium, high, urgent.")
            .When(x => x.Priority is not null);
        RuleFor(x => x.CategoryKey).Matches("^[a-z0-9_]{1,64}$").When(x => x.CategoryKey is not null);
        RuleFor(x => x.EstimatedMinutes).GreaterThan(0).When(x => x.EstimatedMinutes is not null);
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateSubtaskRequestValidator : AbstractValidator<CreateSubtaskRequest>
{
    public CreateSubtaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public sealed class UpdateSubtaskRequestValidator : AbstractValidator<UpdateSubtaskRequest>
{
    public UpdateSubtaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300).When(x => x.Title is not null);
    }
}

public interface ITaskService
{
    Task<PagedResult<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken);

    Task<TaskDto> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<TaskDto> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken);

    Task<TaskDto> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<SubtaskDto> AddSubtaskAsync(Guid userId, Guid taskId, CreateSubtaskRequest request, CancellationToken cancellationToken);

    Task<SubtaskDto> UpdateSubtaskAsync(Guid userId, Guid taskId, Guid subtaskId, UpdateSubtaskRequest request, CancellationToken cancellationToken);

    Task DeleteSubtaskAsync(Guid userId, Guid taskId, Guid subtaskId, CancellationToken cancellationToken);
}
