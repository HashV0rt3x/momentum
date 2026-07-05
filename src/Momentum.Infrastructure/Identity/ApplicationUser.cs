using Microsoft.AspNetCore.Identity;

namespace Momentum.Infrastructure.Identity;

/// <summary>
/// The one Identity user shared by every module (single Identity, per the
/// modular-monolith decision). Modules never reference this type directly — they
/// depend on Momentum.SharedKernel.Security.ICurrentUser for "the current user id"
/// and treat the user as an opaque Guid. Only Momentum.Infrastructure and the
/// Platform.Auth module (which owns registration/login) touch this class.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
