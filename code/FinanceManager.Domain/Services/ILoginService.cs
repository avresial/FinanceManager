using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Domain.Services;

public interface ILoginService
{
    event Action<bool>? LogginStateChanged;
    Task<bool> Login(string username, string password);
    Task<bool> Login(UserSession userSession);
    Task Logout();
    Task<UserSession?> GetLoggedUser();

    /// <summary>
    /// Attempts to obtain a fresh access token from the refresh-token cookie. Returns <c>true</c> when the session
    /// was restored. Safe to call when not logged in (the server simply returns 401).
    /// </summary>
    Task<bool> TryRefresh();
}