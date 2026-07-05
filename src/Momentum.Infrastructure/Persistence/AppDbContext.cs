using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Momentum.Infrastructure.Identity;
using Momentum.SharedKernel.Entities;
using Momentum.SharedKernel.Modules;

namespace Momentum.Infrastructure.Persistence;

/// <summary>
/// The one and only DbContext for the whole modular monolith (one deployable app,
/// one DB, per the locked decisions). Modules never define their own DbContext —
/// they bring EF entity configs (IEntityTypeConfiguration&lt;T&gt;) discovered here
/// via <see cref="ModuleAssemblyScanner"/>, so this class needs zero changes when a
/// new module is added.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private static readonly MethodInfo SetUserQueryFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetUserQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly TimeProvider _timeProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserAccessor currentUserAccessor,
        TimeProvider timeProvider)
        : base(options)
    {
        _currentUserAccessor = currentUserAccessor;
        _timeProvider = timeProvider;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var moduleAssembly in ModuleAssemblyScanner.GetModuleAssemblies())
        {
            builder.ApplyConfigurationsFromAssembly(moduleAssembly);
        }

        // Defense-in-depth global filter: every IUserOwned entity type discovered
        // in the model is restricted to the current user's rows. This is NOT a
        // substitute for explicit `.Where(x => x.UserId == currentUser.UserId)` in
        // module query code — see IUserOwned's doc comment — but it means a missed
        // filter fails closed instead of leaking another user's data.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IUserOwned).IsAssignableFrom(entityType.ClrType) && entityType.ClrType.IsClass)
            {
                SetUserQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    private void StampTimestamps()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    break;
            }
        }
    }

    private void SetUserQueryFilter<TEntity>(ModelBuilder builder) where TEntity : class, IUserOwned
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            _currentUserAccessor.UserId == null || e.UserId == _currentUserAccessor.UserId);
    }
}
