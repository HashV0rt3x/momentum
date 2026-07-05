namespace Momentum.SharedKernel.Exceptions;

/// <summary>
/// Thrown when a requested resource doesn't exist for the current user. Note:
/// because every query filters by UserId, "exists but belongs to someone else"
/// and "doesn't exist" are indistinguishable from the outside — both must throw
/// this, never a 403. That is precisely what keeps user isolation from leaking
/// information across accounts.
/// </summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.", statusCode: 404)
    {
    }

    public NotFoundException(string message) : base(message, statusCode: 404)
    {
    }

    public override string? Code => "not_found";
}
