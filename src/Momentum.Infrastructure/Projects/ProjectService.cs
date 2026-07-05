using Microsoft.EntityFrameworkCore;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Projects;
using Momentum.Domain.Projects;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Projects;

public sealed class ProjectService(AppDbContext dbContext) : IProjectService
{
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto> CreateAsync(Guid userId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = new Project(userId, request.Name, request.Color, request.Description);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid userId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await FindAsync(userId, projectId, cancellationToken);

        project.Name = request.Name ?? project.Name;
        project.Color = request.Color ?? project.Color;
        project.Description = request.Description ?? project.Description;
        project.IsArchived = request.IsArchived ?? project.IsArchived;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task DeleteAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindAsync(userId, projectId, cancellationToken);
        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Project> FindAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
        => await dbContext.Projects
               .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == projectId, cancellationToken)
           ?? throw new NotFoundException("Project", projectId);

    private static ProjectDto ToDto(Project p) => new(
        Id: p.Id.ToString(),
        Name: p.Name,
        Color: p.Color,
        Description: p.Description,
        IsArchived: p.IsArchived,
        MemberIds: [p.UserId.ToString()],
        CreatedAt: p.CreatedAtUtc);
}
