using Momentum.Api.Validation;
using Momentum.Application.Common.Security;
using Momentum.Application.Settings;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 2 — Settings.</summary>
public static class SettingsEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings").WithTags("Settings").RequireAuthorization();

        group.MapGet("", async (
                ICurrentUser currentUser,
                ISettingsService settingsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.GetAsync(currentUser.UserId, cancellationToken)))
            .WithSummary("User preferences (theme, week start, focus timer, notifications).");

        group.MapPatch("", async (
                UpdateSettingsRequest request,
                ICurrentUser currentUser,
                ISettingsService settingsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.UpdateAsync(currentUser.UserId, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateSettingsRequest>>()
            .WithSummary("Update any subset of settings; returns the full settings object.");
    }
}
