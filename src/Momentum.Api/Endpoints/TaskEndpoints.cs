using Momentum.Api.Validation;
using Momentum.Application.Common.Security;
using Momentum.Application.Tasks;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 6 — Tasks + subtasks.</summary>
public static class TaskEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("", async (
                [AsParameters] TaskListQuery query,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            {
                var result = await taskService.ListAsync(currentUser.UserId, query, cancellationToken);

                // Spec: plain array by default, paginated shape only when ?page= is given.
                return query.Page is null ? Results.Ok(result.Items) : Results.Ok(result);
            })
            .WithSummary("List tasks. Filters: status, priority, projectId, assigneeId, categoryKey, dueBefore, q, page, pageSize.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            Results.Ok(await taskService.GetAsync(currentUser.UserId, id, cancellationToken)));

        group.MapPost("", async (
                CreateTaskRequest request,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            {
                var task = await taskService.CreateAsync(currentUser.UserId, request, cancellationToken);
                return Results.Created($"/api/v1/tasks/{task.Id}", task);
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateTaskRequest>>();

        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateTaskRequest request,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            Results.Ok(await taskService.UpdateAsync(currentUser.UserId, id, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateTaskRequest>>();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            {
                await taskService.DeleteAsync(currentUser.UserId, id, cancellationToken);
                return Results.NoContent();
            });

        // ---- Subtasks ----
        group.MapPost("/{id:guid}/subtasks", async (
                Guid id,
                CreateSubtaskRequest request,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            {
                var subtask = await taskService.AddSubtaskAsync(currentUser.UserId, id, request, cancellationToken);
                return Results.Created($"/api/v1/tasks/{id}/subtasks/{subtask.Id}", subtask);
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateSubtaskRequest>>();

        group.MapPatch("/{id:guid}/subtasks/{subId:guid}", async (
                Guid id,
                Guid subId,
                UpdateSubtaskRequest request,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            Results.Ok(await taskService.UpdateSubtaskAsync(currentUser.UserId, id, subId, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateSubtaskRequest>>();

        group.MapDelete("/{id:guid}/subtasks/{subId:guid}", async (
                Guid id,
                Guid subId,
                ICurrentUser currentUser,
                ITaskService taskService,
                CancellationToken cancellationToken) =>
            {
                await taskService.DeleteSubtaskAsync(currentUser.UserId, id, subId, cancellationToken);
                return Results.NoContent();
            });
    }
}
