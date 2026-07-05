using System.Security.Claims;
using Momentum.Infrastructure.Persistence;
using Momentum.SharedKernel.Security;

namespace Momentum.Api.Security;

/// <summary>
/// Single HttpContext-backed implementation satisfying both the strict,
/// module-facing <see cref="ICurrentUser"/> (throws when unauthenticated) and the
/// lenient <see cref="ICurrentUserAccessor"/> used by AppDbContext's query filter
/// (returns null when unauthenticated). Registered once, resolved as both
/// interfaces from the same scoped instance — see Program.cs.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser, ICurrentUserAccessor
{
    public Guid UserId => TryGetUserId()
        ?? throw new InvalidOperationException(
            "No authenticated user in the current request. Endpoint must require authorization.");

    public bool IsAuthenticated => TryGetUserId() is not null;

    Guid? ICurrentUserAccessor.UserId => TryGetUserId();

    private Guid? TryGetUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
