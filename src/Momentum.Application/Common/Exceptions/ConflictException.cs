namespace Momentum.Application.Common.Exceptions;

/// <summary>Thrown on a business-rule conflict, e.g. duplicate registration.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message, statusCode: 409)
    {
    }

    public override string Code => "CONFLICT";
}
