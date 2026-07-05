namespace Momentum.SharedKernel.Exceptions;

/// <summary>
/// Thrown for genuine authorization failures that are NOT "this row belongs to
/// another user" (that case is always a <see cref="NotFoundException"/> — see its
/// doc comment). Reserve this for things like "module disabled for your account"
/// or role/permission checks.
/// </summary>
public sealed class ForbiddenAccessException : AppException
{
    public ForbiddenAccessException(string message) : base(message, statusCode: 403)
    {
    }

    public override string? Code => "forbidden";
}
