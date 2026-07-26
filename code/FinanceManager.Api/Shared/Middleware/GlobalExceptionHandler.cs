using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Shared.Middleware;

/// <summary>
/// Catches any exception that escapes the MVC pipeline and turns it into an RFC-7807
/// <see cref="ProblemDetails"/> response. Stack traces are only exposed in Development so
/// production clients never receive internal implementation detail.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Strip line breaks from the request method/path before logging so a crafted URL can't forge
        // additional log entries (CodeQL: log entries created from user input).
        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            Sanitize(httpContext.Request.Method),
            Sanitize(httpContext.Request.Path.Value));

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Detail = environment.IsDevelopment()
                ? exception.ToString()
                : "An unexpected error occurred while processing your request.",
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}