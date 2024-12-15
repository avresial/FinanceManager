using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Providers;
using FinanceManager.Core.Entities.Login;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Cryptography;
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

        private AuthenticationStateProvider _authState { get; set; }
        public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
            AuthenticationStateProvider AuthState, ILoginRepository loginRepository)
        {
            _sessionStorageService = sessionStorageService;
            _localStorageService = localStorageService;
            this._authState = AuthState;
            _loginRepository = loginRepository;
            _ = _loginRepository.AddUser("Guest", EncryptPassword("GuestPassword"));
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
            return true;
        }
        public async Task<bool> Login(string username, string password)
        {
            username = username.ToLower();
            var encryptedPassword = EncryptPassword(password);

            return await Login(new UserSession()
            {
                UserId = 0,
                UserName = username,
                Password = encryptedPassword
            });
        }

        public async Task Logout()
        {
            await _sessionStorageService.RemoveItemAsync(sessionString);
            await _localStorageService.RemoveItemAsync(sessionString);
            await ((CustomAuthenticationStateProvider)_authState).Logout();
            LoggedUser = null;
        }

        public async Task<bool> AddUser(string login, string password)
        {
            return await _loginRepository.AddUser(login, EncryptPassword(password));
        }

        private string EncryptPassword(string inputString)
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(inputString);
            var hashAlgorithm = SHA256.Create();

            data = hashAlgorithm.ComputeHash(data);
            string hash = System.Text.Encoding.ASCII.GetString(data);

            return hash;
        }
    }
}
