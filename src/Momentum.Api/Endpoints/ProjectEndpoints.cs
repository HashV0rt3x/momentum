using Momentum.Api.Validation;
using Momentum.Application.Common.Security;
using Momentum.Application.Projects;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 4 — Projects.</summary>
public static class ProjectEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/projects").WithTags("Projects").RequireAuthorization();

        group.MapGet("", async (
                ICurrentUser currentUser,
                IProjectService projectService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectService.ListAsync(currentUser.UserId, cancellationToken)));

        group.MapPost("", async (
                CreateProjectRequest request,
                ICurrentUser currentUser,
                IProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                var project = await projectService.CreateAsync(currentUser.UserId, request, cancellationToken);
                return Results.Created($"/api/v1/projects/{project.Id}", project);
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateProjectRequest>>();

        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateProjectRequest request,
                ICurrentUser currentUser,
                IProjectService projectService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectService.UpdateAsync(currentUser.UserId, id, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateProjectRequest>>();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ICurrentUser currentUser,
                IProjectService projectService,
                CancellationToken cancellationToken) =>
            {
                await projectService.DeleteAsync(currentUser.UserId, id, cancellationToken);
                return Results.NoContent();
            });
    }
}
