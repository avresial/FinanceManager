namespace FinanceManager.Api.Middleware;

/// <summary>
/// Emits standard security response headers on every response.
///
/// Headers set (only if not already present, so an upstream proxy can override):
/// <list type="bullet">
///   <item><c>X-Content-Type-Options: nosniff</c></item>
///   <item><c>X-Frame-Options: DENY</c> — kept alongside <c>frame-ancestors</c> for older browsers</item>
///   <item><c>Referrer-Policy: strict-origin-when-cross-origin</c></item>
///   <item><c>Content-Security-Policy</c> — tuned for Blazor WebAssembly: <c>'wasm-unsafe-eval'</c> for
///       the runtime, <c>'unsafe-inline'</c> for the bootstrap scripts and MudBlazor's injected styles,
///       <c>ws:/wss:</c> for SignalR, <c>blob:</c> for downloads and Blazor's workers.</item>
/// </list>
/// HSTS is not handled here — it is wired via <c>UseHsts()</c> in <c>Program.cs</c> so it only
/// applies outside Development (Kestrel won't issue it on plain HTTP either way).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string _contentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "worker-src 'self' blob:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        // Set the headers in OnStarting rather than inline so they are applied right before the
        // response is sent. This survives UseExceptionHandler clearing the response (Response.Clear)
        // when GlobalExceptionHandler turns an unhandled exception into a ProblemDetails result, so
        // error responses carry the same hardening headers as successful ones.
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;

            if (!headers.ContainsKey("X-Content-Type-Options"))
                headers["X-Content-Type-Options"] = "nosniff";

            if (!headers.ContainsKey("X-Frame-Options"))
                headers["X-Frame-Options"] = "DENY";

            if (!headers.ContainsKey("Referrer-Policy"))
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            if (!headers.ContainsKey("Content-Security-Policy"))
                headers["Content-Security-Policy"] = _contentSecurityPolicy;

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}