namespace FinanceManager.Api.Middleware;

internal sealed class RequestBodySizeLimitMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var limit = RequestBodySizeLimits.GetLimitForPath(context.Request.Path);
        if (context.Request.ContentLength > limit)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await next(context);
    }
}
