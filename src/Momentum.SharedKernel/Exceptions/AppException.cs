namespace Momentum.SharedKernel.Exceptions;

/// <summary>
/// Base for exceptions that the global exception handler translates into an RFC
/// 7807 ProblemDetails response with <see cref="StatusCode"/>. Anything else
/// (unhandled) becomes a generic 500 with no internal detail leaked to the client.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    /// <summary>
    /// Optional machine-readable error code surfaced in ProblemDetails.Extensions["code"],
    /// e.g. "habit_not_found". Defaults to null (omitted).
    /// </summary>
    public virtual string? Code => null;
}
