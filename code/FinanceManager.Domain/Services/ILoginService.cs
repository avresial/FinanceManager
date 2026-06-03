using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Services;

public interface ILoginService
{
    event Action<bool>? LogginStateChanged;
    Task<bool> Login(string username, string password);
    Task<bool> Login(UserSession userSession);
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