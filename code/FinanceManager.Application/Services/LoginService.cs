using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Text.Json;

namespace FinanceManager.Application.Services
{
    public class LoginService : ILoginService
    {
        private const string sessionString = "userSession";
        private UserSession? LoggedUser = null;
        private ISessionStorageService _sessionStorageService;
        private ILocalStorageService _localStorageService;
        private readonly ILoginRepository _loginRepository;
        public event Action<bool>? LogginStateChanged;

        private AuthenticationStateProvider _authState { get; set; }
        public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
            AuthenticationStateProvider AuthState, ILoginRepository loginRepository)
        {
            _sessionStorageService = sessionStorageService;
            _localStorageService = localStorageService;
            this._authState = AuthState;
            _loginRepository = loginRepository;
            _ = _loginRepository.AddUser("Guest", PasswordEncryptionProvider.EncryptPassword("GuestPassword"));
        }

        public async Task<UserSession?> GetLoggedUser()
        {
            if (await _localStorageService.ContainKeyAsync(sessionString))
                LoggedUser = await GetKeepMeLoggedinSession();

            return LoggedUser;
        }
        public async Task<UserSession?> GetKeepMeLoggedinSession()
        {
            try
            {
                return await _localStorageService.GetItemAsync<UserSession>(sessionString);
            }
            catch (JsonException ex)
            {
                Console.WriteLine(ex);
                await _localStorageService.RemoveItemAsync(sessionString);
                return null;
            }
        }

        public async Task<bool> Login(UserSession userSession)
        {
            var userFromDatabase = await _loginRepository.GetUser(userSession.UserName, userSession.Password);
            if (userFromDatabase is null)
            {
                Console.WriteLine($"INFO - {userSession.UserName} user was not found.");
                await Logout();
                return false;
            }

            LoggedUser = userSession;

            await _sessionStorageService.SetItemAsync(sessionString, LoggedUser);
            await _localStorageService.SetItemAsync(sessionString, LoggedUser);
            var authState = await ((CustomAuthenticationStateProvider)_authState).ChangeUser(userSession.UserName, userSession.UserName, "Associate");
            LogginStateChanged?.Invoke(true);
            return true;
        }
        public async Task<bool> Login(string username, string password)
        {
            username = username.ToLower();

            var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(password);
            var loginResult = await Login(new UserSession()
            {
                UserId = 0,
                UserName = username,
                Password = encryptedPassword
            });

            LogginStateChanged?.Invoke(loginResult);

            return loginResult;
        }

        public async Task Logout()
        {
            await _sessionStorageService.RemoveItemAsync(sessionString);
            await _localStorageService.RemoveItemAsync(sessionString);
            await ((CustomAuthenticationStateProvider)_authState).Logout();
            LogginStateChanged?.Invoke(false);
            LoggedUser = null;
        }
    }
}
