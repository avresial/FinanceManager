using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;

namespace FinanceManager.Api.Middleware;

internal sealed class RequestBodySizeLimitMiddleware(
    RequestDelegate next,
    ILogger<RequestBodySizeLimitMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var limit = RequestBodySizeLimits.GetLimitForPath(context.Request.Path);
        var canHaveBody = context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody ?? false;
        if (canHaveBody && context.Request.ContentLength is long contentLength && contentLength > limit)
        {
            var sanitizedPath = SanitizeForLog(context.Request.Path.Value);
            logger.LogWarning(
                "Rejected request to {Path} because Content-Length {ContentLength} exceeded limit {Limit}.",
                sanitizedPath,
                contentLength,
                limit);
            await WriteRejectionAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body exceeds size limit.");
            return;
        }

        await next(context);
    }

    private static string SanitizeForLog(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }

    private static async Task WriteRejectionAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(payload);
    }
}