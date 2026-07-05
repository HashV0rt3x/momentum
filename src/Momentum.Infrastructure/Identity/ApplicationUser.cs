using Microsoft.AspNetCore.Identity;
using Momentum.Domain.Users;

namespace Momentum.Infrastructure.Identity;

/// <summary>
/// The one Identity user. Application code never references this type directly —
/// it depends on Momentum.Application.Common.Security.ICurrentUser for "the
/// current user id" and on IUserService/IAuthService for profile data.
/// Sign-in is Telegram-only (no passwords): <see cref="TelegramId"/> is the
/// stable external identity; IdentityUser.UserName is a synthetic unique value.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Telegram numeric user id — the stable external identity. Unique.</summary>
    public long TelegramId { get; set; }

    /// <summary>Telegram @username (may change or be absent).</summary>
    public string? TelegramUsername { get; set; }

    /// <summary>Display name shown in the UI (from Telegram first/last name, editable).</summary>
    public string Name { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? JobTitle { get; set; }

    public UserRole Role { get; set; } = UserRole.Member;

    /// <summary>uz | ru | en.</summary>
    public string Locale { get; set; } = "uz";

    public string TimeZoneId { get; set; } = "Asia/Tashkent";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
