namespace FinanceManager.Api.Middleware;

// Adds a baseline set of security response headers to every request. Kept as explicit middleware
// (rather than per-endpoint attributes) so the headers also cover static Blazor assets and the SPA fallback.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await next(context);
    }
}