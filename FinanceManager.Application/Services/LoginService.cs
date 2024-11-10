using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Core.Entities.Login;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class LoginService : ILoginService
    {
        private const string sessionString = "userSession";
        private UserSession? LoggedUser = null;
        private ISessionStorageService _sessionStorageService;
        private ILocalStorageService _localStorageService;

        public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService)
        {
            _sessionStorageService = sessionStorageService;
            _localStorageService = localStorageService;
        }

        public async Task<UserSession?> GetLoggedUser()
        {
            return LoggedUser;
        }
        public async Task<UserSession?> GetKeepMeLoggedinSession()
        {
            return await _localStorageService.GetItemAsync<UserSession>(sessionString);
        }
        public async Task<bool> Login(string username, string password)
        {
            LoggedUser = new UserSession()
            {
                UserId = 0,
                UserName = username,
            };

            await _sessionStorageService.SetItemAsync<UserSession>(sessionString, LoggedUser);
            await _localStorageService.SetItemAsync<UserSession>(sessionString, LoggedUser);

            return true;
        }

        public async Task Logout()
        {
            await _sessionStorageService.RemoveItemAsync(sessionString);
            await _localStorageService.RemoveItemAsync(sessionString);
            LoggedUser = null;
        }
    }
}
