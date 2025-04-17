using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Providers;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Components.Services;

public class LoginService : ILoginService
{
    private const string _sessionString = "userSession";
    private UserSession? _loggedUser = null;
    private ISessionStorageService _sessionStorageService;
    private ILocalStorageService _localStorageService;
    private readonly IUserRepository _loginRepository;
    private readonly HttpClient _httpClient;

    public event Action<bool>? LogginStateChanged;

    private AuthenticationStateProvider _authState { get; set; }
    public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
        AuthenticationStateProvider AuthState, IUserRepository loginRepository, HttpClient httpClient)
    {
        _sessionStorageService = sessionStorageService;
        _localStorageService = localStorageService;
        _authState = AuthState;

        _loginRepository = loginRepository;
        _httpClient = httpClient;
        _ = _loginRepository.AddUser("Guest", PasswordEncryptionProvider.EncryptPassword("GuestPassword"), PricingLevel.Basic);
    }

    public async Task<UserSession?> GetLoggedUser()
    {
        if (await _localStorageService.ContainKeyAsync(_sessionString))
            _loggedUser = await GetKeepMeLoggedInSession();

        return _loggedUser;
    }
    public async Task<UserSession?> GetKeepMeLoggedInSession()
    {
        try
        {
            return await _localStorageService.GetItemAsync<UserSession>(_sessionString);
        }
        catch (JsonException ex)
        {
            Console.WriteLine(ex);
            await _localStorageService.RemoveItemAsync(_sessionString);
            return null;
        }
    }

    public async Task<bool> Login(UserSession userSession)
    {
        LoginRequestModel loginRequestModel = new LoginRequestModel(userSession.UserName, userSession.Password);
        LoginResponseModel? result = null;
        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/Login",
                JsonHelper.GenerateStringContent(JsonHelper.SerializeObj(loginRequestModel)));
            result = await response.Content.ReadFromJsonAsync<LoginResponseModel>();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        if (result is null) return false;

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
        userSession.Token = result.AccessToken;
        userSession.UserId = result.UserId;
        _loggedUser = userSession;

        await _sessionStorageService.SetItemAsync(_sessionString, _loggedUser);
        await _localStorageService.SetItemAsync(_sessionString, _loggedUser);
        var authState = await ((CustomAuthenticationStateProvider)_authState).ChangeUser(userSession.UserName, userSession.UserId.ToString(), "Associate");
        LogginStateChanged?.Invoke(true);
        return true;
    }
    public async Task<bool> Login(string username, string password)
    {
        username = username.ToLower();
        var encryptedPassword = "";
        //if (username != "guest")
        encryptedPassword = PasswordEncryptionProvider.EncryptPassword(password);
        //else
        //    encryptedPassword = password;

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
        await _sessionStorageService.RemoveItemAsync(_sessionString);
        await _localStorageService.RemoveItemAsync(_sessionString);
        await ((CustomAuthenticationStateProvider)_authState).Logout();
        LogginStateChanged?.Invoke(false);
        _loggedUser = null;
    }
}
