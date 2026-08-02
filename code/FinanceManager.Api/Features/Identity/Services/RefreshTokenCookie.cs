namespace FinanceManager.Api.Features.Identity.Services;

/// <summary>
/// Centralises how the refresh-token cookie is written and cleared so the login and refresh endpoints stay
/// consistent. The cookie is HttpOnly + SameSite=Strict and scoped to <c>/api/Auth</c> so it is only ever sent to
/// the refresh and logout endpoints — never exposed to the WASM bundle or any other API route.
///
/// Because the client therefore cannot see it, a companion <see cref="PresenceCookieName"/> cookie is written
/// alongside it: readable by script, scoped to the whole site, and carrying no token material. It exists purely so
/// a signed-out visitor can tell there is nothing to restore and skip the refresh round-trip, which would
/// otherwise spend two of the strict auth rate-limit permits on every load of the login page.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Path = "/api/Auth";

    /// <summary>
    /// Name of the script-readable companion cookie marking that a refresh token exists. It is a presence hint
    /// only — it holds no token material and must never be treated as an authentication or authorisation signal
    /// server-side, since anything readable by script is equally writable by it.
    /// </summary>
    public const string PresenceCookieName = "fm_has_session";

    private const string _presencePath = "/";
    private const string _presenceValue = "1";

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

        // Mirrors the refresh cookie's lifetime and scheme handling, but is readable by script and scoped to "/"
        // so the app can check it from any route before deciding whether a session restore is worth attempting.
        response.Cookies.Append(PresenceCookieName, _presenceValue, new CookieOptions
        {
            HttpOnly = false,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = _presencePath,
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

        // The delete options must match how the cookie was written, or the browser keeps the original and the
        // client goes on believing a session is still restorable.
        response.Cookies.Delete(PresenceCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = _presencePath,
            IsEssential = true,
        });
    }
}