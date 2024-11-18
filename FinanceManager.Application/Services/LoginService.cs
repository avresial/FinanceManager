using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Providers;
using FinanceManager.Core.Entities.Login;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace FinanceManager.Application.Services
{
    public class LoginService : ILoginService
    {
        private const string sessionString = "userSession";
        private UserSession? LoggedUser = null;
        private ISessionStorageService _sessionStorageService;
        private ILocalStorageService _localStorageService;
        private AuthenticationStateProvider _authState { get; set; }
        public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, AuthenticationStateProvider AuthState)
        {
            _sessionStorageService = sessionStorageService;
            _localStorageService = localStorageService;
            this._authState = AuthState;
        }

        public async Task<UserSession?> GetLoggedUser()
        {
            if (await _localStorageService.ContainKeyAsync(sessionString))
                LoggedUser = await GetKeepMeLoggedinSession();

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
            var authState = await ((CustomAuthenticationStateProvider)_authState).ChangeUser(username, username, "Associate");
            return true;
        }

        public async Task Logout()
        {
            await _sessionStorageService.RemoveItemAsync(sessionString);
            await _localStorageService.RemoveItemAsync(sessionString);
            await ((CustomAuthenticationStateProvider)_authState).Logout();
            LoggedUser = null;
        }
    }
}
