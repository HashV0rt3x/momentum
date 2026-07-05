namespace Momentum.SharedKernel.Entities;

/// <summary>
/// Base class for the overwhelming majority of entities in this system: anything
/// tied to a single user's data (habits, tasks, journal entries, transactions, ...).
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
