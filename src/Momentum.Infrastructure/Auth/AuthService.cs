using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Momentum.Application.Auth;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Users;
using Momentum.Domain.Auth;
using Momentum.Domain.Categories;
using Momentum.Domain.Users;
using Momentum.Infrastructure.Identity;
using Momentum.Infrastructure.Persistence;
using Momentum.Infrastructure.Users;

namespace Momentum.Infrastructure.Auth;

public sealed class AuthService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITelegramLoginVerifier telegramVerifier,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    private static readonly string[] SupportedLocales = ["uz", "ru", "en"];

    public async Task<AuthResponse> LoginWithTelegramAsync(
        TelegramLoginRequest request,
        string? preferredLocale,
        CancellationToken cancellationToken)
    {
        var telegramUser = telegramVerifier.Verify(request);

        return await LoginOrRegisterAsync(telegramUser, preferredLocale, cancellationToken);
    }

    public async Task<AuthResponse> LoginWithTelegramCodeAsync(
        string code,
        string? preferredLocale,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var loginCode = await dbContext.TelegramLoginCodes
            .Where(c => c.Code == code && c.UsedAtUtc == null && c.ExpiresAtUtc > now)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (loginCode is null)
        {
            throw new UnauthorizedException("Login code is invalid or expired.", "LOGIN_CODE_INVALID");
        }

        loginCode.MarkUsed(now); // persisted by the SaveChanges inside IssueRefreshTokenAsync

        var telegramUser = new TelegramUserData(
            loginCode.TelegramId,
            loginCode.FirstName,
            loginCode.LastName,
            loginCode.Username,
            PhotoUrl: null,
            loginCode.LanguageCode);

        return await LoginOrRegisterAsync(telegramUser, preferredLocale, cancellationToken);
    }

    private async Task<AuthResponse> LoginOrRegisterAsync(
        TelegramUserData telegramUser,
        string? preferredLocale,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramUser.TelegramId, cancellationToken);

        if (user is null)
        {
            user = await CreateUserAsync(telegramUser, preferredLocale, cancellationToken);
        }
        else
        {
            await SyncProfileFromTelegramAsync(user, telegramUser);
        }

        var (access, expiresIn) = jwtTokenService.CreateAccessToken(
            user.Id, user.Role.ToString().ToLowerInvariant(), user.Name);
        var refresh = await IssueRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResponse(user.ToDto(), access, refresh, expiresIn);
    }

    public async Task<TokenPairResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = HashToken(refreshToken);
        var now = timeProvider.GetUtcNow();

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || !stored.IsActive(now))
        {
            throw new UnauthorizedException("Refresh token is invalid or expired.", "REFRESH_TOKEN_INVALID");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString())
            ?? throw new UnauthorizedException("Refresh token is invalid or expired.", "REFRESH_TOKEN_INVALID");

        // Rotation: the presented token is single-use.
        stored.Revoke(now);

        var (access, expiresIn) = jwtTokenService.CreateAccessToken(
            user.Id, user.Role.ToString().ToLowerInvariant(), user.Name);
        var newRefresh = await IssueRefreshTokenAsync(user.Id, cancellationToken);

        return new TokenPairResponse(access, newRefresh, expiresIn);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = HashToken(refreshToken);

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, cancellationToken);

        if (stored is not null)
        {
            stored.Revoke(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(
        TelegramUserData telegramUser,
        string? preferredLocale,
        CancellationToken cancellationToken)
    {
        // First user in the workspace becomes owner; everyone after is a member.
        var isFirstUser = !await userManager.Users.AnyAsync(cancellationToken);

        var user = new ApplicationUser
        {
            // Synthetic, guaranteed-unique username; the Telegram @username is
            // display data and may change or collide after Identity normalization.
            UserName = $"tg_{telegramUser.TelegramId}",
            TelegramId = telegramUser.TelegramId,
            TelegramUsername = telegramUser.Username,
            Name = BuildDisplayName(telegramUser),
            AvatarUrl = telegramUser.PhotoUrl,
            Role = isFirstUser ? UserRole.Owner : UserRole.Member,
            Locale = ResolveLocale(telegramUser.LanguageCode, preferredLocale),
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(
                $"Could not create user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        SeedDefaultCategories(user.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <summary>
    /// Default per-user categories (stable keys the frontend localizes —
    /// spec section 5).
    /// </summary>
    private void SeedDefaultCategories(Guid userId)
    {
        (string Key, string Color, string Icon)[] defaults =
        [
            ("work", "#4F80E1", "Briefcase"),
            ("deep_work", "#3E63C8", "Brain"),
            ("meetings", "#B4653F", "Users"),
            ("learning", "#3FB07E", "BookOpen"),
            ("personal", "#9A5FB5", "Heart"),
        ];

        foreach (var (key, color, icon) in defaults)
        {
            dbContext.Categories.Add(new Category(userId, key, color, icon));
        }
    }

    private async Task SyncProfileFromTelegramAsync(ApplicationUser user, TelegramUserData telegramUser)
    {
        // Keep volatile Telegram profile data fresh on every login, but never
        // overwrite a Name the user has customized via PATCH /me.
        var changed = false;

        if (user.TelegramUsername != telegramUser.Username)
        {
            user.TelegramUsername = telegramUser.Username;
            changed = true;
        }

        if (telegramUser.PhotoUrl is not null && user.AvatarUrl != telegramUser.PhotoUrl)
        {
            user.AvatarUrl = telegramUser.PhotoUrl;
            changed = true;
        }

        if (changed)
        {
            await userManager.UpdateAsync(user);
        }
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var token = new RefreshToken(
            userId,
            HashToken(raw),
            timeProvider.GetUtcNow().AddDays(jwtOptions.Value.RefreshDays));

        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        return raw;
    }

    private static string HashToken(string raw)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string BuildDisplayName(TelegramUserData telegramUser)
        => string.IsNullOrWhiteSpace(telegramUser.LastName)
            ? telegramUser.FirstName
            : $"{telegramUser.FirstName} {telegramUser.LastName}";

    private static string ResolveLocale(string? telegramLanguageCode, string? preferredLocale)
    {
        if (telegramLanguageCode is not null && SupportedLocales.Contains(telegramLanguageCode))
        {
            return telegramLanguageCode;
        }

        if (preferredLocale is not null && SupportedLocales.Contains(preferredLocale))
        {
            return preferredLocale;
        }

        return "uz";
    }
}
