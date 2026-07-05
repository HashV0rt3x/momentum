using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Momentum.SharedKernel.Exceptions;

namespace Momentum.SharedKernel.Validation;

/// <summary>
/// Minimal-API endpoint filter that validates <typeparamref name="TRequest"/> with
/// its registered FluentValidation <see cref="IValidator{T}"/> before the handler
/// runs. Usage in a module's endpoint mapping:
///
///   group.MapPost("/", Handler).AddEndpointFilter&lt;ValidationEndpointFilter&lt;CreateHabitRequest&gt;&gt;();
///
/// Throws <see cref="AppValidationException"/> on failure, which the global
/// exception handler renders as a 400 ProblemDetails. If no validator is
/// registered for TRequest, validation is silently skipped (not every DTO needs one).
/// </summary>
public sealed class ValidationEndpointFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next(context);
        }

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var validationResult = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            throw new AppValidationException(validationResult.Errors);
        }

        return await next(context);
    }
}
