using Microsoft.Extensions.Primitives;
using Momentum.Api.Validation;
using Momentum.Application.Auth;
using Momentum.Application.Common.Security;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 1 — Telegram-only authentication.</summary>
public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/telegram", async (
                TelegramLoginRequest request,
                HttpContext httpContext,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var response = await authService.LoginWithTelegramAsync(
                    request,
                    ResolvePreferredLocale(httpContext),
                    cancellationToken);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .AddEndpointFilter<ValidationEndpointFilter<TelegramLoginRequest>>()
            .WithSummary("Sign in / sign up with Telegram (widget or Mini App initData).");

        group.MapPost("/telegram/code", async (
                TelegramCodeLoginRequest request,
                HttpContext httpContext,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var response = await authService.LoginWithTelegramCodeAsync(
                    request.Code,
                    ResolvePreferredLocale(httpContext),
                    cancellationToken);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .AddEndpointFilter<ValidationEndpointFilter<TelegramCodeLoginRequest>>()
            .WithSummary("Sign in / sign up with the 6-digit code the bot sent in response to /start.");

        group.MapPost("/refresh", async (
                RefreshTokenRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            Results.Ok(await authService.RefreshAsync(request.RefreshToken, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .AddEndpointFilter<ValidationEndpointFilter<RefreshTokenRequest>>()
            .WithSummary("Exchange a refresh token for a new token pair (rotation).");

        group.MapPost("/logout", async (
                RefreshTokenRequest request,
                ICurrentUser currentUser,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.LogoutAsync(currentUser.UserId, request.RefreshToken, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationEndpointFilter<RefreshTokenRequest>>()
            .WithSummary("Invalidate the given refresh token.");
    }

    /// <summary>
    /// Spec i18n priority for server-chosen locale on first login:
    /// ?locale= query → Accept-Language header → (service falls back to "uz").
    /// </summary>
    private static string? ResolvePreferredLocale(HttpContext httpContext)
    {
        if (httpContext.Request.Query.TryGetValue("locale", out StringValues fromQuery)
            && !StringValues.IsNullOrEmpty(fromQuery))
        {
            return fromQuery.ToString();
        }

        var acceptLanguage = httpContext.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return null;
        }

        // "uz-UZ,uz;q=0.9,en;q=0.8" -> "uz"
        var first = acceptLanguage.Split(',')[0].Split(';')[0].Trim();
        return first.Length >= 2 ? first[..2].ToLowerInvariant() : null;
    }
}
