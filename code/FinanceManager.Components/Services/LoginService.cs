using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Providers;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class LoginService : ILoginService
{
    private const string _sessionString = "userSession";
    private UserSession? _loggedUser = null;
    private readonly ISessionStorageService _sessionStorageService;
    private readonly ILocalStorageService _localStorageService;
    private readonly IUserRepository _loginRepository;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Once we've tried (and failed) to silently restore a session we don't keep hammering the refresh endpoint on
    // every GetLoggedUser call, and an explicit logout flips this on so we never auto-revive a deliberately ended session.
    private bool _initialRefreshAttempted;

    // Holds the single in-flight initial refresh so concurrent first-load callers all await the same restore
    // instead of the second caller seeing the "attempted" flag and returning a null (unauthenticated) session.
    private Task? _initialRefreshTask;

    public event Action<bool>? LogginStateChanged;

    private readonly AuthenticationStateProvider _authStateProvider;
    public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
        AuthenticationStateProvider authState, IUserRepository loginRepository, HttpClient httpClient)
    {
        _sessionStorageService = sessionStorageService;
        _localStorageService = localStorageService;
        _authStateProvider = authState;

        _loginRepository = loginRepository;
        _httpClient = httpClient;
        _ = _loginRepository.AddUser("Guest", PasswordEncryptionProvider.EncryptPassword("GuestPassword"), PricingLevel.Basic, UserRole.User);
    }

    public async Task<UserSession?> GetLoggedUser()
    {
        // On a fresh page load nothing is in memory yet; try once to revive the session from the refresh cookie so
        // deep links keep working after a reload without bouncing through the login page. Concurrent callers share
        // the same in-flight task, and the "attempted" flag is only set once that task has actually completed — so
        // no caller can observe a null session while the restore is still running.
        if (_loggedUser is null && !_initialRefreshAttempted)
        {
            _initialRefreshTask ??= InitialRefresh();
            await _initialRefreshTask;
        }

        return _loggedUser;
    }

    private async Task InitialRefresh()
    {
        try
        {
            await TryRefresh();
        }
        finally
        {
            _initialRefreshAttempted = true;
        }
    }

    public async Task<bool> Login(UserSession userSession)
    {
        LoginRequestModel loginRequestModel = new(userSession.UserName, userSession.Password);
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

        await ApplySession(result, userSession);

        LogginStateChanged?.Invoke(true);
        return true;
    }
    public async Task<bool> Login(string username, string password)
    {
        var loginResult = await Login(new()
        {
            UserId = 0,
            UserName = username.ToLower(),
            Password = PasswordEncryptionProvider.EncryptPassword(password),
            UserRole = UserRole.User,
        });

        LogginStateChanged?.Invoke(loginResult);

        return loginResult;
    }

    public async Task<bool> TryRefresh()
    {
        // Serialise concurrent refreshes (e.g. a burst of dashboard cards all hitting a 401 at once) so we only
        // rotate the refresh token once and the rest observe the freshly restored session.
        await _refreshLock.WaitAsync();
        try
        {
            using var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/Auth/refresh", content: null);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponseModel>();
            if (result is null) return false;

            await ApplySession(result);
            LogginStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task Logout()
    {
        try
        {
            using var _ = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/Auth/logout", content: null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        await _sessionStorageService.RemoveItemAsync(_sessionString);
        await _localStorageService.RemoveItemAsync(_sessionString);
        _httpClient.DefaultRequestHeaders.Authorization = null;
        await ((CustomAuthenticationStateProvider)_authStateProvider).Logout();
        LogginStateChanged?.Invoke(false);
        _loggedUser = null;
        // Block GetLoggedUser from silently re-authenticating with a stale cookie after a deliberate logout.
        _initialRefreshAttempted = true;
    }

    // Applies a successful login/refresh response: updates the in-memory session, the bearer header used by the
    // typed HttpClients, and the cascading authentication state. The refresh token itself lives only in the
    // server-set HttpOnly cookie, so nothing sensitive is written to browser storage here.
    private async Task ApplySession(LoginResponseModel result, UserSession? existing = null)
    {
        var session = existing ?? new UserSession
        {
            UserId = result.UserId,
            UserName = result.UserName,
            Password = string.Empty,
            UserRole = result.UserRole,
        };

        session.UserId = result.UserId;
        session.UserName = result.UserName;
        session.UserRole = result.UserRole;
        session.Token = result.AccessToken;

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
        _loggedUser = session;
        _initialRefreshAttempted = true;

        await ((CustomAuthenticationStateProvider)_authStateProvider)
            .ChangeUser(session.UserName, session.UserId.ToString(), session.UserRole.ToString());
    }
}