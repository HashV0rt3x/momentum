namespace Momentum.Domain.Common;

/// <summary>
/// Base class for the overwhelming majority of entities in this system: anything
/// tied to a single user's data (tasks, time entries, goals, ...).
/// </summary>
public abstract class UserOwnedEntity : Entity, IUserOwned
{
    public Guid UserId { get; protected set; }

    protected UserOwnedEntity()
    {
    }

    protected UserOwnedEntity(Guid userId)
    {
        UserId = userId;
    }
}
