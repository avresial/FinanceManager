using System.Text.Json;

namespace FinanceManager.Api.Middleware;

internal sealed class RequestBodySizeLimitMiddleware(
    RequestDelegate next,
    ILogger<RequestBodySizeLimitMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var limit = RequestBodySizeLimits.GetLimitForPath(context.Request.Path);
        if (context.Request.ContentLength is null)
        {
            logger.LogWarning("Rejected request to {Path} because Content-Length was missing.", context.Request.Path);
            await WriteRejectionAsync(context, StatusCodes.Status411LengthRequired, "Request body requires a Content-Length header.");
            return;
        }

        if (context.Request.ContentLength > limit)
        {
            logger.LogWarning(
                "Rejected request to {Path} because Content-Length {ContentLength} exceeded limit {Limit}.",
                context.Request.Path,
                context.Request.ContentLength,
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