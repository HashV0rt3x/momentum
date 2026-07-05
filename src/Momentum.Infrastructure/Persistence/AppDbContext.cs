using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Momentum.Domain.Auth;
using Momentum.Domain.Categories;
using Momentum.Domain.Common;
using Momentum.Domain.Focus;
using Momentum.Domain.Projects;
using Momentum.Domain.Settings;
using Momentum.Domain.Tasks;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Persistence;

/// <summary>
/// The one and only DbContext (one deployable app, one DB). Entity
/// configurations live in Persistence/Configurations and are discovered via
/// ApplyConfigurationsFromAssembly.
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

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TelegramLoginCode> TelegramLoginCodes => Set<TelegramLoginCode>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Defense-in-depth global filter: every IUserOwned entity type discovered
        // in the model is restricted to the current user's rows. This is NOT a
        // substitute for explicit `.Where(x => x.UserId == currentUser.UserId)` in
        // application query code — see IUserOwned's doc comment — but it means a
        // missed filter fails closed instead of leaking another user's data.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IUserOwned).IsAssignableFrom(entityType.ClrType) && entityType.ClrType.IsClass)
            {
                SetUserQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
            }
        }
    }

    // Overriding the (bool, CancellationToken) overloads covers every save path:
    // the parameterless/CT-only variants delegate to these in the base class, so
    // timestamps are stamped no matter which overload a caller picks.
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
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
