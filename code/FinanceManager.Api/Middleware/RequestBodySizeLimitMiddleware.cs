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
            logger.LogWarning(
                "Rejected request to {Path} because Content-Length {ContentLength} exceeded limit {Limit}.",
                context.Request.Path,
                contentLength,
                limit);
            await WriteRejectionAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body exceeds size limit.");
            return;
        }

        await next(context);
    }

    private static async Task WriteRejectionAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(payload);
    }
}