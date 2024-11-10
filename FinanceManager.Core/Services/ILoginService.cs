using FinanceManager.Core.Entities.Login;

namespace FinanceManager.Core.Services
{
    public interface ILoginService
    {
        Task<bool> Login(string username, string password);
        Task Logout();
        Task<UserSession?> GetLoggedUser();
        Task<UserSession?> GetKeepMeLoggedinSession();
    }
}
