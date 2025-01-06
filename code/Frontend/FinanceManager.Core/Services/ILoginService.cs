using FinanceManager.Core.Entities.Login;

namespace FinanceManager.Core.Services
{
    public interface ILoginService
    {
        event Action<bool>? LogginStateChanged;
        Task<bool> Login(string username, string password);
        Task<bool> Login(UserSession userSession);
        Task Logout();
        Task<bool> AddUser(string login, string password);
        Task<UserSession?> GetLoggedUser();
        Task<UserSession?> GetKeepMeLoggedinSession();
    }
}
