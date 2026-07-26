namespace FinanceManager.Api.Features.Identity.Services;

/// <summary>
/// Centralises how the refresh-token cookie is written and cleared so the login and refresh endpoints stay
/// consistent. The cookie is HttpOnly + SameSite=Strict and scoped to <c>/api/Auth</c> so it is only ever sent to
/// the refresh and logout endpoints — never exposed to the WASM bundle or any other API route.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Path = "/api/Auth";

    public static void Append(HttpResponse response, bool isHttps, string cookieName, string token, DateTimeOffset expires)
    {
        response.Cookies.Append(cookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Secure tracks the scheme so the cookie still round-trips over plain HTTP in tests while staying
            // Secure in production (which is served over HTTPS).
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expires,
            IsEssential = true,
        });
    }

    public static void Delete(HttpResponse response, bool isHttps, string cookieName)
    {
        response.Cookies.Delete(cookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            IsEssential = true,
        });
    }
}