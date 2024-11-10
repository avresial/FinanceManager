using FinanceManager.Core.Entities.Login;

namespace FinanceManager.Core.Services
{
    public interface ILoginService
    {
        public Task<bool> Login(string username, string password);
        public Task<UserSession?> GetLoggedUser();
    }
}
