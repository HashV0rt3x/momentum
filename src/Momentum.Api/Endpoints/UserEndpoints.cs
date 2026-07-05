using Momentum.Api.Validation;
using Momentum.Application.Common.Security;
using Momentum.Application.Users;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 2 — Account (/me).</summary>
public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/me").WithTags("Account").RequireAuthorization();

        group.MapGet("", async (
                ICurrentUser currentUser,
                IUserService userService,
                CancellationToken cancellationToken) =>
            Results.Ok(await userService.GetMeAsync(currentUser.UserId, cancellationToken)))
            .WithSummary("Current authenticated user.");

        group.MapPatch("", async (
                UpdateMeRequest request,
                ICurrentUser currentUser,
                IUserService userService,
                CancellationToken cancellationToken) =>
            Results.Ok(await userService.UpdateMeAsync(currentUser.UserId, request, cancellationToken)))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateMeRequest>>()
            .WithSummary("Update profile (any subset of name, jobTitle, timezone, locale).");
    }
}
