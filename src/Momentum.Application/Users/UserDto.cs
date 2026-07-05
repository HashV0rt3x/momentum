namespace Momentum.Application.Users;

/// <summary>Spec `User` shape (section 1/2 of the API spec).</summary>
public sealed record UserDto(
    string Id,
    long TelegramId,
    string Name,
    string? Username,
    string? AvatarUrl,
    string? Email,
    string Role,
    string? JobTitle,
    string Locale,
    string Timezone,
    DateTimeOffset CreatedAt);
