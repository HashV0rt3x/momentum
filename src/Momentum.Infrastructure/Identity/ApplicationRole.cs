using Microsoft.AspNetCore.Identity;

namespace Momentum.Infrastructure.Identity;

/// <summary>
/// Not expected to be used heavily for a single-tenant personal app, but kept so
/// Identity's role infrastructure (and any future "admin" style flag) is available
/// without a schema change.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
}
