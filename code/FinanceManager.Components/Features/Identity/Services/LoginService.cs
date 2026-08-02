using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Identity;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Identity.Services;

public class LoginService : ILoginService
{
    // Mirrors RefreshTokenCookie.PresenceCookieName on the API side. Duplicated across the project boundary the
    // same way the antiforgery cookie name already is, since Components does not reference the API project.
    public const string SessionPresenceCookieName = "fm_has_session";

    private const string _sessionString = "userSession";

    // Marks that this browser has already made the one-time attempt to restore a pre-presence-cookie session.
    // Deliberately survives logout: it records a migration step, not session state.
    private const string _legacySessionProbedKey = "legacySessionProbed";
    private UserSession? _loggedUser = null;
    private readonly ISessionStorageService _sessionStorageService;
    private readonly ILocalStorageService _localStorageService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoginService> _logger;
    private readonly IAntiforgeryTokenService _antiforgeryTokenService;
    private readonly IBrowserCookieReader _browserCookieReader;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Once we've tried (and failed) to silently restore a session we don't keep hammering the refresh endpoint on
    // every GetLoggedUser call, and an explicit logout flips this on so we never auto-revive a deliberately ended session.
    private bool _initialRefreshAttempted;

    // Holds the single in-flight initial refresh so concurrent first-load callers all await the same restore
    // instead of the second caller seeing the "attempted" flag and returning a null (unauthenticated) session.
    private Task? _initialRefreshTask;

    // Sequence number and outcome of the last completed refresh. A caller snapshots the sequence before it
    // queues on _refreshLock; if the sequence has moved by the time the lock is acquired, another caller
    // already performed the refresh this one was about to duplicate, so its outcome is adopted instead.
    private int _refreshSequence;
    private bool _lastRefreshSucceeded;

    public event Action<bool>? LogginStateChanged;

    private readonly AuthenticationStateProvider _authStateProvider;
    public LoginService(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
        AuthenticationStateProvider authState, HttpClient httpClient,
        IAntiforgeryTokenService antiforgeryTokenService, IBrowserCookieReader browserCookieReader,
        ILogger<LoginService> logger)
    {
        _sessionStorageService = sessionStorageService;
        _localStorageService = localStorageService;
        _authStateProvider = authState;

        _httpClient = httpClient;
        _antiforgeryTokenService = antiforgeryTokenService;
        _browserCookieReader = browserCookieReader;
        _logger = logger;
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
            if (!await ShouldAttemptRestore())
                return;

            await TryRefresh();
        }
        finally
        {
            _initialRefreshAttempted = true;
        }
    }

    private async Task<bool> ShouldAttemptRestore()
    {
        // Sessions issued before the presence cookie existed carry a valid refresh token but no hint, and a
        // refresh token stays valid for weeks — so trusting the hint immediately would silently sign those users
        // out on their next visit. Probing once per browser lets them restore normally and re-issues the hint as
        // part of the rotation; every load after that consults the hint and costs nothing.
        if (!await HasProbedForLegacySession())
            return true;

        return await HasRestorableSession();
    }

    private async Task<bool> HasProbedForLegacySession()
    {
        try
        {
            if (await _localStorageService.ContainKeyAsync(_legacySessionProbedKey))
                return true;

            await _localStorageService.SetItemAsync(_legacySessionProbedKey, true);
            return false;
        }
        catch (Exception ex)
        {
            // Treating the probe as already done keeps a storage failure from reinstating the per-load refresh
            // for good; the hint below still governs, and it is correct for every session issued from now on.
            _logger.LogWarning(ex, "Could not record the legacy-session probe; relying on the presence cookie.");
            return true;
        }
    }

    // The refresh token itself is HttpOnly, so the client cannot see whether one exists. The server writes a
    // script-readable companion cookie alongside it purely as a presence hint, letting a signed-out visitor skip
    // a refresh that can only ever come back 401 — a round-trip that costs two of the strict auth rate-limit
    // permits, enough that a handful of login-page loads alone would trip the limit.
    private async Task<bool> HasRestorableSession()
    {
        try
        {
            return await _browserCookieReader.Exists(SessionPresenceCookieName);
        }
        catch (Exception ex)
        {
            // Fail open: if the hint cannot be read the session may still be restorable, and a redundant request
            // is far cheaper than stranding a signed-in user on the login page.
            _logger.LogWarning(ex, "Could not read the session presence cookie; attempting a refresh anyway.");
            return true;
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
            if (!response.IsSuccessStatusCode) return false;
            result = await response.Content.ReadFromJsonAsync<LoginResponseModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login request.");
        }

        if (result is null) return false;

        // Don't let the plaintext password ride along on the session we keep in memory as _loggedUser; it's only
        // needed for the request above.
        userSession.Password = string.Empty;
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
            // Send the plaintext password; the API is the single place that hashes it (see UserController/LoginController).
            Password = password,
            UserRole = UserRole.User,
        });

        LogginStateChanged?.Invoke(loginResult);

        return loginResult;
    }

    public async Task<bool> DevelopLogin(string login)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/DevelopLogin/{Uri.EscapeDataString(login)}", null);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponseModel>();
            if (result is null) return false;

            await ApplySession(result);
            LogginStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during develop login request.");
            return false;
        }
    }

    public async Task<bool> TryRefresh()
    {
        // Coalesce concurrent refreshes (e.g. a burst of dashboard cards all hitting a 401 at once) so we only
        // rotate the refresh token once and the rest observe the freshly restored session. Snapshot the sequence
        // *before* queueing on the lock: every caller that was already waiting when the winner completed adopts
        // its outcome instead of issuing a redundant refresh, which would otherwise burn the strict auth
        // rate-limit budget and end the session with a 429 the moment an access token expires.
        var observedSequence = Volatile.Read(ref _refreshSequence);

        await _refreshLock.WaitAsync();
        try
        {
            if (Volatile.Read(ref _refreshSequence) != observedSequence)
                return _lastRefreshSucceeded;

            _lastRefreshSucceeded = await SendRefreshRequest();

            // Only the lock holder advances the sequence, and only after the refresh it performed has settled,
            // so a caller arriving after this point still gets a fresh refresh rather than a stale outcome.
            Interlocked.Increment(ref _refreshSequence);
            return _lastRefreshSucceeded;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // Performs the actual refresh round-trip. Never throws: a failure here means "the session could not be
    // restored", which callers translate into ending the session rather than an error surfaced to the user.
    private async Task<bool> SendRefreshRequest()
    {
        try
        {
            using var request = await _antiforgeryTokenService.CreateRequest(
                HttpMethod.Post, "api/Auth/refresh", CancellationToken.None);
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponseModel>();
            if (result is null) return false;

            await ApplySession(result);
            LogginStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing session.");
            return false;
        }
    }

    public async Task Logout()
    {
        try
        {
            using var request = await _antiforgeryTokenService.CreateRequest(
                HttpMethod.Post, "api/Auth/logout", CancellationToken.None);
            using var _ = await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout request.");
        }

        await EndSession();
    }

    public async Task EndSession()
    {
        await _sessionStorageService.RemoveItemAsync(_sessionString);
        await _localStorageService.RemoveItemAsync(_sessionString);
        _httpClient.DefaultRequestHeaders.Authorization = null;
        await ((CustomAuthenticationStateProvider)_authStateProvider).Logout();
        _loggedUser = null;
        _antiforgeryTokenService.ClearToken();
        // Block GetLoggedUser from silently re-authenticating with a stale cookie after the session has ended.
        _initialRefreshAttempted = true;
        LogginStateChanged?.Invoke(false);
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
        _antiforgeryTokenService.ClearToken();

        await ((CustomAuthenticationStateProvider)_authStateProvider)
            .ChangeUser(session.UserName, session.UserId.ToString(), session.UserRole);
    }
}