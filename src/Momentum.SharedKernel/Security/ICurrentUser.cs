namespace Momentum.SharedKernel.Security;

/// <summary>
/// The one and only way module code should learn "who is asking". Concrete
/// implementation lives in Momentum.Api (it needs IHttpContextAccessor + claims),
/// registered as scoped in DI. Modules depend only on this interface, never on
/// HttpContext or Identity types directly — that keeps every module's query layer
/// testable and keeps the user-isolation rule enforceable in one place.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The authenticated user's id. Throws <see cref="InvalidOperationException"/>
    /// if called with no authenticated user — callers must sit behind
    /// [Authorize]/RequireAuthorization(), which every module endpoint (other than
    /// /auth/register, /auth/login, /auth/refresh) must do.
    /// </summary>
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
