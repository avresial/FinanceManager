using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.User;

public partial class UserSettingsPage : ComponentBase, IBrowserViewportObserver, IAsyncDisposable
{
    private const string _requiredDeleteConfirmation = "delete my account";

    private Breakpoint _currentBrakePoint;
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private UserSession? _loggedUser;
    private Domain.Entities.Users.User? _userData;

    private bool _isLoadingPage;
    private bool _success;
    private string _selectedPlan = $"{PricingLevel.Free}";
    private string? _confirmPassword { get; set; }
    private string? _deleteConfirmation;
    private MudForm? _passwordForm;
    private MudTextField<string>? _passwordField;
    private List<string> _plans = [$"{PricingLevel.Free}", $"{PricingLevel.Basic}", $"{PricingLevel.Premium}"];
    private RecordCapacity? _recordCapacity;



    private int _ActiveIndex;
    public int ActiveIndex
    {
        get { return _ActiveIndex; }
        set
        {
            if (_ActiveIndex == value)
                return;
            _errors.Clear();
            _warnings.Clear();
            _info.Clear();
            _ActiveIndex = value;
        }
    }

    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }
    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    Guid IBrowserViewportObserver.Id { get; } = Guid.NewGuid();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BrowserViewportService.SubscribeAsync(this, fireImmediately: true);
        }

        await base.OnAfterRenderAsync(firstRender);
    }


    protected override async Task OnInitializedAsync()
    {
        _isLoadingPage = true;
        _loggedUser = await LoginService.GetLoggedUser();
        if (_loggedUser is null)
        {
            _isLoadingPage = false;
            return;
        }

        _userData = await UserService.GetUser(_loggedUser.UserId);
        if (_userData is null)
        {
            _isLoadingPage = false;
            return;
        }

        if (_plans.Contains(_userData.PricingLevel.ToString()))
            _selectedPlan = _userData.PricingLevel.ToString();

        try
        {
            _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
        }
        catch (Exception ex)
        {
            _errors.Insert(0, ex.Message);
        }
        _isLoadingPage = false;
    }
    public async ValueTask DisposeAsync() => await BrowserViewportService.UnsubscribeAsync(this);
    private string PasswordMatch(string arg)
    {
        if (_passwordField is not null && _passwordField.Value != arg)
            return "Passwords don't match";

        return "";
    }
    private static IEnumerable<string> PasswordStrength(string pw)
    {

        if (string.IsNullOrWhiteSpace(pw))
        {
            yield return "Password is required!";
            yield break;
        }

#if DEBUG

        yield break;
#else
        if (pw.Length < 8)
            yield return "Password must be at least of length 8";
        if (!Regex.IsMatch(pw, @"[A-Z]"))
            yield return "Password must contain at least one capital letter";
        if (!Regex.IsMatch(pw, @"[a-z]"))
            yield return "Password must contain at least one lowercase letter";
        if (!Regex.IsMatch(pw, @"[0-9]"))
            yield return "Password must contain at least one digit";
#endif
    }
    private async Task UpgradePricingPlan()
    {
        if (_loggedUser is null) return;

        var result = await UserService.UpdatePricingPlan(_loggedUser.UserId, (PricingLevel)Enum.Parse(typeof(PricingLevel), _selectedPlan));
        _errors.Clear();
        if (!result)
        {
            _errors.Insert(0, "Failed to change plan.");
        }
        else
        {
            _info.Insert(0, $"Successfully upgraded plan to {_selectedPlan}");

            _userData = await UserService.GetUser(_loggedUser.UserId);
            if (_userData is null) return;

            try
            {
                _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
            }
            catch (Exception ex)
            {
                _errors.Insert(0, ex.Message);
            }
        }

    }

    private async Task DeleteMyAccount()
    {
        if (_loggedUser is null) return;
        var result = await UserService.Delete(_loggedUser.UserId);
        if (!result)
        {
            _errors.Insert(0, "Failed to remove user.");
            return;
        }
        else
        {
            _errors.Clear();
            await LoginService.Logout();
            NavigationManager.NavigateTo("/");
        }
    }
    private async Task ChangePasswordAsync()
    {
        if (_loggedUser is null) return;
        if (_passwordForm is null) return;
        if (_passwordField is null) return;
        if (string.IsNullOrEmpty(_confirmPassword)) return;

        await _passwordForm.Validate();
        if (_passwordForm.IsValid)
        {
            if (_passwordField.Value != _confirmPassword)
            {
                _warnings.Insert(0, "New passwords do not match.");
                return;
            }
            else
            {
                var result = await UserService.UpdatePassword(_loggedUser.UserId, _confirmPassword);
                if (!result)
                {
                    _errors.Insert(0, "Failed to change password.");
                    return;
                }
                else
                {
                    _errors.Clear();
                    _info.Insert(0, "Password changed successfully.");
                }
            }
        }
        _confirmPassword = "";
    }
    private Color GetStorageIndicatorColor()
    {
        if (_recordCapacity is not null && _recordCapacity.GetStorageUsedPercentage() >= 80)
            return Color.Error;
        return Color.Primary;
    }

    public async Task NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
    {
        if (browserViewportEventArgs.IsImmediate)
            _currentBrakePoint = browserViewportEventArgs.Breakpoint;
        else
            _currentBrakePoint = browserViewportEventArgs.Breakpoint;

        await InvokeAsync(StateHasChanged);
    }

}