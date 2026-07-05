using Microsoft.AspNetCore.Diagnostics;
using Momentum.Application.Common.Exceptions;

namespace Momentum.Api.ExceptionHandling;

/// <summary>
/// Single place every unhandled exception in the pipeline funnels through,
/// producing the API spec's error shape:
///   { "status": 400, "message": "...", "code": "VALIDATION_ERROR", "errors": {...}? }
/// Recognized <see cref="AppException"/> subclasses map to their declared status
/// code; anything else becomes a generic 500 with no internal detail leaked.
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int status;
        string message;
        string code;
        IReadOnlyDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case AppValidationException validationException:
                status = validationException.StatusCode;
                message = validationException.Message;
                code = validationException.Code;
                errors = validationException.Errors;
                logger.LogWarning(
                    "Validation failed for {Method} {Path}: {@Errors}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    validationException.Errors);
                break;

            case AppException appException:
                status = appException.StatusCode;
                message = appException.Message;
                code = appException.Code;
                logger.LogWarning(
                    appException,
                    "Handled {ExceptionType} ({Code}) for {Method} {Path}",
                    appException.GetType().Name,
                    appException.Code,
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                message = "An unexpected error occurred.";
                code = "INTERNAL_ERROR";
                logger.LogError(
                    exception,
                    "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                break;
        }

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(status, message, code, errors),
            cancellationToken);

        return true;
    }

    private sealed record ErrorResponse(
        int Status,
        string Message,
        string Code,
        IReadOnlyDictionary<string, string[]>? Errors);
}
