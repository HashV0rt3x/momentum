using FluentValidation;

namespace Momentum.Application.Projects;

/// <summary>Spec section 4. memberIds is [ownerId] until team support lands.</summary>
public sealed record ProjectDto(
    string Id,
    string Name,
    string Color,
    string? Description,
    bool IsArchived,
    IReadOnlyList<string> MemberIds,
    DateTimeOffset CreatedAt);

public sealed record CreateProjectRequest(string Name, string Color, string? Description);

/// <summary>PATCH — nulls mean "leave unchanged".</summary>
public sealed record UpdateProjectRequest(string? Name, string? Color, string? Description, bool? IsArchived);

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9A-Fa-f]{6}$");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProjectDto> CreateAsync(Guid userId, CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDto> UpdateAsync(Guid userId, Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}
