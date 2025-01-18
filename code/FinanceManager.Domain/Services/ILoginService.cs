using FinanceManager.Domain.Entities.Login;

namespace FinanceManager.Domain.Services
{
    public interface ILoginService
    {
        event Action<bool>? LogginStateChanged;
        Task<bool> Login(string username, string password);
        Task<bool> Login(UserSession userSession);
        Task Logout();
        Task<UserSession?> GetLoggedUser();
        Task<UserSession?> GetKeepMeLoggedinSession();
    }
}
