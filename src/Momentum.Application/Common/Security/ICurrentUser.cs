namespace Momentum.Application.Common.Security;

/// <summary>
/// The one and only way application code should learn "who is asking". Concrete
/// implementation lives in Momentum.Api (it needs IHttpContextAccessor + claims),
/// registered as scoped in DI. Application code depends only on this interface,
/// never on HttpContext or Identity types directly.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The authenticated user's id. Throws <see cref="InvalidOperationException"/>
    /// if called with no authenticated user — callers must sit behind
    /// RequireAuthorization().
    /// </summary>
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
