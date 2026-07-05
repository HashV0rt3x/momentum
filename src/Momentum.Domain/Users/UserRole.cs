namespace Momentum.Domain.Users;

/// <summary>
/// Workspace role, per the API spec (owner | admin | member | guest).
/// Serialized to the client as a lowercase string.
/// </summary>
public enum UserRole
{
    Owner,
    Admin,
    Member,
    Guest,
}
