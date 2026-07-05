using FluentValidation.Results;

namespace Momentum.SharedKernel.Exceptions;

/// <summary>
/// Carries FluentValidation failures out to the global exception handler, which
/// renders them as an RFC 7807 ProblemDetails with an "errors" extension shaped
/// like ASP.NET Core's built-in ValidationProblemDetails (field name -> message[]).
/// Raised by <see cref="Momentum.SharedKernel.Validation.ValidationEndpointFilter{TRequest}"/>
/// so every module gets consistent 400 responses without writing this by hand.
/// </summary>
public sealed class AppValidationException : AppException
{
    public AppValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.", statusCode: 400)
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override string? Code => "validation_failed";
}
