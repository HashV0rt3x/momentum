namespace Momentum.Application.Common.Exceptions;

/// <summary>
/// 401 with a spec error code, e.g. TELEGRAM_SIGNATURE_INVALID,
/// TELEGRAM_AUTH_EXPIRED, REFRESH_TOKEN_INVALID.
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string code = "UNAUTHORIZED")
        : base(message, statusCode: 401)
    {
        Code = code;
    }

    public override string Code { get; }
}
