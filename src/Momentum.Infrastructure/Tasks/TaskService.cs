using Microsoft.EntityFrameworkCore;
using Momentum.Application.Common;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Tasks;
using Momentum.Domain.Tasks;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Tasks;

public sealed class TaskService(AppDbContext dbContext, TimeProvider timeProvider) : ITaskService
{
    public async Task<PagedResult<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = dbContext.Tasks
            .Include(t => t.Subtasks)
            .Where(t => t.UserId == userId);

        if (query.Status is not null && TaskEnumMapping.TryParseStatus(query.Status, out var status))
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (query.Priority is not null && TaskEnumMapping.TryParsePriority(query.Priority, out var priority))
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        if (query.ProjectId is not null)
        {
            tasks = tasks.Where(t => t.ProjectId == query.ProjectId);
        }

        if (query.AssigneeId is not null)
        {
            tasks = tasks.Where(t => t.AssigneeIds.Contains(query.AssigneeId.Value));
        }

        if (query.CategoryKey is not null)
        {
            tasks = tasks.Where(t => t.CategoryKey == query.CategoryKey);
        }

        if (query.DueBefore is not null)
        {
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate < query.DueBefore);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim()}%";
            tasks = tasks.Where(t =>
                EF.Functions.ILike(t.Title, pattern)
                || (t.Description != null && EF.Functions.ILike(t.Description, pattern)));
        }

        tasks = tasks.OrderByDescending(t => t.CreatedAtUtc);

        var paging = new PagedRequest { Page = query.Page ?? 1, PageSize = query.PageSize ?? 50 };

        var total = await tasks.CountAsync(cancellationToken);
        var items = await tasks
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<TaskDto>.Create(
            items.Select(ToDto).ToList(), paging.Page, paging.PageSize, total);
    }

    public async Task<TaskDto> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
        => ToDto(await FindAsync(userId, taskId, cancellationToken));

    public async Task<TaskDto> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        await EnsureProjectExistsAsync(userId, request.ProjectId, cancellationToken);

        var task = new TaskItem(userId, request.Title)
        {
            Description = request.Description,
            CategoryKey = request.CategoryKey,
            ProjectId = request.ProjectId,
            AssigneeIds = request.AssigneeIds?.ToList() ?? [userId],
            Tags = request.Tags?.ToList() ?? [],
            EstimatedMinutes = request.EstimatedMinutes,
            DueDate = request.DueDate,
            StartDate = request.StartDate,
        };

        if (request.Status is not null && TaskEnumMapping.TryParseStatus(request.Status, out var status))
        {
            task.Status = status;
            task.CompletedAt = status == TaskItemStatus.Done ? timeProvider.GetUtcNow() : null;
        }

        if (request.Priority is not null && TaskEnumMapping.TryParsePriority(request.Priority, out var priority))
        {
            task.Priority = priority;
        }

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(task);
    }

    public async Task<TaskDto> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await FindAsync(userId, taskId, cancellationToken);

        if (request.ProjectId is not null)
        {
            await EnsureProjectExistsAsync(userId, request.ProjectId, cancellationToken);
            task.ProjectId = request.ProjectId;
        }

        task.Title = request.Title ?? task.Title;
        task.Description = request.Description ?? task.Description;
        task.CategoryKey = request.CategoryKey ?? task.CategoryKey;
        task.AssigneeIds = request.AssigneeIds?.ToList() ?? task.AssigneeIds;
        task.Tags = request.Tags?.ToList() ?? task.Tags;
        task.EstimatedMinutes = request.EstimatedMinutes ?? task.EstimatedMinutes;
        task.DueDate = request.DueDate ?? task.DueDate;
        task.StartDate = request.StartDate ?? task.StartDate;

        if (request.Priority is not null && TaskEnumMapping.TryParsePriority(request.Priority, out var priority))
        {
            task.Priority = priority;
        }

        if (request.Status is not null && TaskEnumMapping.TryParseStatus(request.Status, out var status)
            && status != task.Status)
        {
            task.Status = status;
            // Spec: setting status "done" sets completedAt server-side.
            task.CompletedAt = status == TaskItemStatus.Done ? timeProvider.GetUtcNow() : null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(task);
    }

    public async Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await FindAsync(userId, taskId, cancellationToken);
        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubtaskDto> AddSubtaskAsync(Guid userId, Guid taskId, CreateSubtaskRequest request, CancellationToken cancellationToken)
    {
        var task = await FindAsync(userId, taskId, cancellationToken);

        var subtask = new Subtask(task.Id, request.Title);
        task.Subtasks.Add(subtask);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubtaskDto(subtask.Id.ToString(), subtask.Title, subtask.Done);
    }

    public async Task<SubtaskDto> UpdateSubtaskAsync(Guid userId, Guid taskId, Guid subtaskId, UpdateSubtaskRequest request, CancellationToken cancellationToken)
    {
        var task = await FindAsync(userId, taskId, cancellationToken);

        var subtask = task.Subtasks.FirstOrDefault(s => s.Id == subtaskId)
            ?? throw new NotFoundException("Subtask", subtaskId);

        subtask.Title = request.Title ?? subtask.Title;
        subtask.Done = request.Done ?? subtask.Done;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubtaskDto(subtask.Id.ToString(), subtask.Title, subtask.Done);
    }

    public async Task DeleteSubtaskAsync(Guid userId, Guid taskId, Guid subtaskId, CancellationToken cancellationToken)
    {
        var task = await FindAsync(userId, taskId, cancellationToken);

        var subtask = task.Subtasks.FirstOrDefault(s => s.Id == subtaskId)
            ?? throw new NotFoundException("Subtask", subtaskId);

        task.Subtasks.Remove(subtask);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TaskItem> FindAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
        => await dbContext.Tasks
               .Include(t => t.Subtasks)
               .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == taskId, cancellationToken)
           ?? throw new NotFoundException("Task", taskId);

    private async Task EnsureProjectExistsAsync(Guid userId, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return;
        }

        var exists = await dbContext.Projects
            .AnyAsync(p => p.UserId == userId && p.Id == projectId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Project", projectId);
        }
    }

    private static TaskDto ToDto(TaskItem t) => new(
        Id: t.Id.ToString(),
        Title: t.Title,
        Description: t.Description,
        Status: t.Status.ToWire(),
        Priority: t.Priority.ToWire(),
        ProjectId: t.ProjectId?.ToString(),
        CategoryKey: t.CategoryKey,
        AssigneeIds: t.AssigneeIds.Select(id => id.ToString()).ToList(),
        Tags: t.Tags,
        Subtasks: t.Subtasks
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => new SubtaskDto(s.Id.ToString(), s.Title, s.Done))
            .ToList(),
        EstimatedMinutes: t.EstimatedMinutes,
        TrackedMinutes: 0, // computed from time entries once that module lands
        DueDate: t.DueDate,
        StartDate: t.StartDate,
        CompletedAt: t.CompletedAt,
        CreatedAt: t.CreatedAtUtc,
        UpdatedAt: t.UpdatedAtUtc);
}
