using Momentum.Domain.Common;

namespace Momentum.Domain.Projects;

/// <summary>Spec section 4. Owned by one user; memberIds surfaces as [ownerId] for now.</summary>
public sealed class Project : UserOwnedEntity
{
    private Project()
    {
    }

    public Project(Guid userId, string name, string color, string? description) : base(userId)
    {
        Name = name;
        Color = color;
        Description = description;
    }

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsArchived { get; set; }
}
