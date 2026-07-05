using Momentum.Api.Validation;
using Momentum.Application.Common.Security;
using Momentum.Application.Focus;

namespace Momentum.Api.Endpoints;

/// <summary>Spec section 8 — Focus Sessions (Pomodoro).</summary>
public static class FocusSessionEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/focus-sessions").WithTags("Focus Sessions").RequireAuthorization();

        group.MapGet("", async (
                [AsParameters] FocusSessionListQuery query,
                ICurrentUser currentUser,
                IFocusSessionService focusService,
                CancellationToken cancellationToken) =>
            Results.Ok(await focusService.ListAsync(currentUser.UserId, query, cancellationToken)))
            .WithSummary("List sessions. ?date=today (user's timezone) or ?from=&to= ISO range; default today.");

        group.MapPost("", async (
                CreateFocusSessionRequest request,
                ICurrentUser currentUser,
                IFocusSessionService focusService,
                CancellationToken cancellationToken) =>
            {
                var session = await focusService.CreateAsync(currentUser.UserId, request, cancellationToken);
                return Results.Created($"/api/v1/focus-sessions/{session.Id}", session);
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateFocusSessionRequest>>()
            .WithSummary("Record a finished or aborted Pomodoro round.");
    }
}
