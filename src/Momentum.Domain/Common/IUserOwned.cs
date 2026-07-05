namespace Momentum.Domain.Common;

/// <summary>
/// Marks an entity as belonging to exactly one user. Every entity except truly
/// global/reference data must implement this. Infrastructure uses this interface
/// to build global EF query filters, and application query/service code must
/// filter by it explicitly as well — the query filter is defense in depth, not a
/// substitute for filtering in application code.
/// </summary>
public interface IUserOwned
{
    Guid UserId { get; }
}
