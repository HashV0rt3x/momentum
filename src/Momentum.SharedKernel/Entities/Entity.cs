namespace Momentum.SharedKernel.Entities;

/// <summary>
/// Base class for every persisted entity in every module. Provides a stable,
/// index-friendly primary key and audit timestamps. Timestamps are stamped
/// automatically by the DbContext's SaveChanges override (see Momentum.Infrastructure),
/// not by callers.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAtUtc { get; internal set; }

    public DateTimeOffset UpdatedAtUtc { get; internal set; }
}
