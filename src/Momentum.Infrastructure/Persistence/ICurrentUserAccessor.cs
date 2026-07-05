namespace Momentum.Infrastructure.Persistence;

/// <summary>
/// Lenient, nullable-returning counterpart to Momentum.SharedKernel.Security.ICurrentUser,
/// used only by AppDbContext to build the global per-entity query filter (defense
/// in depth — module query/service code must still filter by UserId explicitly;
/// see IUserOwned's doc comment). Nullable so this works with no HTTP context at
/// all: EF design-time tooling (`dotnet ef migrations add`) and any future
/// background worker that hasn't been given a user yet.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}

/// <summary>Default registration target for contexts with no authenticated request (design-time, workers).</summary>
public sealed class NullCurrentUserAccessor : ICurrentUserAccessor
{
    public Guid? UserId => null;
}
