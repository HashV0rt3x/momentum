using Momentum.Application.Users;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Users;

public static class UserMapping
{
    public static UserDto ToDto(this ApplicationUser user) => new(
        Id: user.Id.ToString(),
        TelegramId: user.TelegramId,
        Name: user.Name,
        Username: user.TelegramUsername,
        AvatarUrl: user.AvatarUrl,
        Email: user.Email,
        Role: user.Role.ToString().ToLowerInvariant(),
        JobTitle: user.JobTitle,
        Locale: user.Locale,
        Timezone: user.TimeZoneId,
        CreatedAt: user.CreatedAtUtc);
}
