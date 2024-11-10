using Blazored.SessionStorage;
using FinanceManager.Core.Entities.Login;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class LoginService : ILoginService
    {
        private UserSession? LoggedUser;
        private ISessionStorageService _sessionStorageService;

        public LoginService(ISessionStorageService sessionStorageService)
        {
            _sessionStorageService = sessionStorageService;
        }

        public async Task<UserSession?> GetLoggedUser()
        {
            return LoggedUser;
        }

        public async Task<bool> Login(string username, string password)
        {
            LoggedUser = new UserSession()
            {
                UserId = 0,
                UserName = username,
            };

            await _sessionStorageService.SetItemAsync<UserSession>("userSession", LoggedUser);

            return true;
        }
    }
}
