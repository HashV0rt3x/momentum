using FluentValidation.Results;

namespace Momentum.Application.Common.Exceptions;

/// <summary>
/// Carries FluentValidation failures out to the global exception handler, which
/// renders them as a 400 with an "errors" map (field name -> message[]). Raised
/// by the API layer's ValidationEndpointFilter so every endpoint gets consistent
/// 400 responses without writing this by hand.
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

    public override string Code => "VALIDATION_ERROR";
}
