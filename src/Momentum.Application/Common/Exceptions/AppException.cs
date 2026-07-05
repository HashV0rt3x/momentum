namespace Momentum.Application.Common.Exceptions;

/// <summary>
/// Base for exceptions that the global exception handler translates into the
/// spec's error shape: <c>{ "status": ..., "message": ..., "code": ... }</c>.
/// Anything else (unhandled) becomes a generic 500 with no internal detail
/// leaked to the client.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    /// <summary>Machine-readable error code per the API spec, e.g. "VALIDATION_ERROR".</summary>
    public abstract string Code { get; }
}
