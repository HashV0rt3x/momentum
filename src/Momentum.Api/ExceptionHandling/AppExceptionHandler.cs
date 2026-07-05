using Microsoft.AspNetCore.Diagnostics;
using Momentum.SharedKernel.Exceptions;

namespace Momentum.Api.ExceptionHandling;

/// <summary>
/// Single place every unhandled exception in the pipeline funnels through,
/// producing an RFC 7807 ProblemDetails response. Recognized <see cref="AppException"/>
/// subclasses map to their declared status code with their message as the title;
/// anything else becomes a generic 500 with no internal detail leaked. Registered
/// via builder.Services.AddExceptionHandler&lt;AppExceptionHandler&gt;() and
/// activated by app.UseExceptionHandler() in Program.cs.
/// </summary>
public sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
        };

        switch (exception)
        {
            case AppValidationException validationException:
                problemDetails.Status = validationException.StatusCode;
                problemDetails.Title = validationException.Message;
                problemDetails.Type = $"https://httpstatuses.io/{validationException.StatusCode}";
                problemDetails.Extensions["code"] = validationException.Code;
                problemDetails.Extensions["errors"] = validationException.Errors;
                logger.LogWarning(
                    "Validation failed for {Method} {Path}: {@Errors}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    validationException.Errors);
                break;

            case AppException appException:
                problemDetails.Status = appException.StatusCode;
                problemDetails.Title = appException.Message;
                problemDetails.Type = $"https://httpstatuses.io/{appException.StatusCode}";
                problemDetails.Extensions["code"] = appException.Code;
                logger.LogWarning(
                    appException,
                    "Handled {ExceptionType} for {Method} {Path}",
                    appException.GetType().Name,
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "An unexpected error occurred.";
                problemDetails.Type = "https://httpstatuses.io/500";
                logger.LogError(
                    exception,
                    "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }
}
