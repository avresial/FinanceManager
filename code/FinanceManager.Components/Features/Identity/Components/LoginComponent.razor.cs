using Blazored.LocalStorage;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceManager.Components.Features.Identity.Components;

public partial class LoginComponent
{
    private const string _guestLogin = "Guest";
    private bool _success;
    private string[] _errors = [];

    // Login failure is shown separately from _errors: _errors is two-way bound to the MudForm's field validation
    // (@bind-Errors) and gets overwritten whenever the form re-validates, which would silently clobber this message.
    private string? _loginError;
    private MudForm? _form;
    private LoginModel _loginModel = new();
    private bool _isProcessing = false;

    [Inject] public required ILogger<LoginComponent> Logger { get; set; }
    [Inject] public required IConfiguration Configuration { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILocalStorageService LocalStorageService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }

    [Parameter] public string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
#if DEBUG
        //await LoginService.Login("guest", "GuestPassword");
        //Navigation.NavigateTo("");

        //Navigation.NavigateTo("UserSettings");

        //await LoginService.Login("admin", "admin");
        //Navigation.NavigateTo("Admin/Dashboard");
        //return;
#endif

        await NotifyIfSessionExpired();

        bool firstVisit = !(await LocalStorageService.ContainKeyAsync("isThisFirstVisit"));
        if (firstVisit && string.IsNullOrWhiteSpace(ReturnUrl))
        {
            await LocalStorageService.SetItemAsync("isThisFirstVisit", true);
            Navigation.NavigateTo("landingpage");
            return;
        }

        // GetLoggedUser silently restores the session from the refresh-token cookie when one is still valid, so a
        // user returning within the refresh window lands straight back in the app without re-entering credentials.
        var loggedUser = await LoginService.GetLoggedUser();
        if (loggedUser is not null)
        {
            if (await ContinueOAuth(loggedUser))
                return;

            Navigation.NavigateTo("");
        }
    }

    private async Task NotifyIfSessionExpired()
    {
        if (!await LocalStorageService.ContainKeyAsync(TokenRefreshRedirectHandler.SessionExpiredKey))
            return;

        await LocalStorageService.RemoveItemAsync(TokenRefreshRedirectHandler.SessionExpiredKey);
        Snackbar.Add("Your session has expired, please sign in again.", Severity.Info);
    }

    private async Task Login()
    {
        if (_loginModel.Login is null || _loginModel.Password is null) return;
        if (_form is null) return;

        if (_isProcessing) return;
        _isProcessing = true;
        _loginError = null;

        try
        {
            await _form.Validate();

            var loginResult = await LoginService.Login(_loginModel.Login, _loginModel.Password);

            if (loginResult.IsSuccess)
            {
                var loggedUser = await LoginService.GetLoggedUser();
                if (loggedUser is null) return;
                if (await ContinueOAuth(loggedUser))
                    return;

                switch (loggedUser.UserRole)
                {
                    case Domain.Identity.Entities.UserRole.User:
                        Navigation.NavigateTo("");
                        break;
                    case Domain.Identity.Entities.UserRole.Admin:
                        Navigation.NavigateTo("Admin/Dashboard");
                        break;
                    default:
                        Navigation.NavigateTo("");
                        break;
                }

                return;
            }

            _loginError = DescribeFailure(loginResult);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private static string DescribeFailure(LoginResult result) => result.Status switch
    {
        LoginResultStatus.RateLimited => DescribeRateLimit(result.RetryAfter),
        LoginResultStatus.LockedOut => string.IsNullOrWhiteSpace(result.Message)
            ? "This account is temporarily locked due to repeated failed login attempts. Please try again later."
            : result.Message,
        LoginResultStatus.Error => "Something went wrong while signing in. Please try again.",
        _ => "Incorrect username or password.",
    };

    private static string DescribeRateLimit(TimeSpan? retryAfter)
    {
        // Only quote a countdown when the server actually told us how long to wait; otherwise stay generic rather
        // than inventing a "1 second" from a missing/zero Retry-After.
        if (retryAfter is not TimeSpan wait || wait <= TimeSpan.Zero)
            return "Too many attempts. Please wait a moment and try again.";

        var seconds = (int)Math.Ceiling(wait.TotalSeconds);
        return $"Too many attempts. Please try again in {seconds} second{(seconds == 1 ? "" : "s")}.";
    }

    private async Task<bool> ContinueOAuth(UserSession user)
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl) || string.IsNullOrWhiteSpace(user.Token))
            return false;

        await JSRuntime.InvokeVoidAsync("financeManager.postOAuthBridge", user.Token, ReturnUrl);
        return true;
    }


    private async Task LogGuest()
    {
        try
        {
            FinancialAccountService.InitializeMock();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }

        var guestPassword = Configuration["GuestAccount:Password"] ?? "GuestPassword";
        await LoginService.Login(_guestLogin, guestPassword);
        Navigation.NavigateTo("");
    }

    private async Task OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await Login();
        }
    }
}