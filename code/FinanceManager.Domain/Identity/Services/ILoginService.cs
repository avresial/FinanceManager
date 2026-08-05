using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Services;

public interface ILoginService
{
    event Action<bool>? LogginStateChanged;
    Task<LoginResult> Login(string username, string password);
    Task<LoginResult> Login(UserSession userSession);

    /// <summary>
    /// Passwordless develop-only login ("guest" or "testuser") through the develop login endpoint. Returns
    /// <c>false</c> when the endpoint is unavailable (Production/Release) or the login is not supported.
    /// </summary>
    Task<bool> DevelopLogin(string login);
    Task Logout();

    /// <summary>
    /// Tears down the local session without notifying the server: clears the in-memory user, the bearer header,
    /// browser storage and the cascading authentication state. Used by the 401 handler when a refresh fails — the
    /// access token is already dead, so there is nothing to revoke server-side, but the in-memory state must be
    /// invalidated or the app keeps treating the user as authenticated and loops between the app and the login page.
    /// </summary>
    Task EndSession();
    Task<UserSession?> GetLoggedUser();

    /// <summary>
    /// Attempts to obtain a fresh access token from the refresh-token cookie. Returns <c>true</c> when the session
    /// was restored. Safe to call when not logged in (the server simply returns 401).
    /// </summary>
    Task<bool> TryRefresh();
}