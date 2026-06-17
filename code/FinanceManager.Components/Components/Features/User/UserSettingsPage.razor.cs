using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.User;

public partial class UserSettingsPage : ComponentBase
{
    private const string _requiredDeleteConfirmation = "delete my account";

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private UserSession? _loggedUser;
    private Domain.Identity.Entities.User? _userData;
    private RecordCapacity? _recordCapacity;

    private bool _isLoadingPage;
    private bool _isDirty;
    private bool _passwordValid;

    private string _displayName = string.Empty;
    private string _email = string.Empty;
    private string _initialDisplayName = string.Empty;

    private string? _currentPassword;
    private string? _newPassword;
    private string? _confirmPassword;
    private MudForm? _passwordForm;
    private MudTextField<string>? _passwordField;

    private string _selectedPlan = $"{PricingLevel.Free}";
    private string _initialPlan = $"{PricingLevel.Free}";
    private string? _selectedSection = "profile";
    private string? _deleteConfirmation;

    private readonly List<PlanOption> _planOptions =
    [
        new(PricingLevel.Free, "Beginner", "Free", ["No currency synchronization", $"{PricingProvider.GetMaxAllowedEntries(PricingLevel.Free)} transactions", $"Up to {PricingProvider.GetMaxAccountCount(PricingLevel.Free)} accounts"]),
        new(PricingLevel.Basic, "Starter", "$10 / mo", ["No currency synchronization", $"{PricingProvider.GetMaxAllowedEntries(PricingLevel.Basic)} transactions", $"Up to {PricingProvider.GetMaxAccountCount(PricingLevel.Basic)} accounts"]),
        new(PricingLevel.Premium, "Professional", "$15 / mo", ["Currency synchronization", $"{PricingProvider.GetMaxAllowedEntries(PricingLevel.Premium)} transactions", $"Up to {PricingProvider.GetMaxAccountCount(PricingLevel.Premium)} accounts"]),
    ];

    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }

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

        _displayName = _userData.Login;
        _email = _userData.Login;
        _initialDisplayName = _displayName;
        _selectedPlan = _userData.PricingLevel.ToString();
        _initialPlan = _selectedPlan;

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

    private void MarkDirty() => _isDirty = HasChanges();

    private bool HasChanges()
    {
        if (_displayName != _initialDisplayName) return true;
        if (_selectedPlan != _initialPlan) return true;
        if (!string.IsNullOrEmpty(_currentPassword)) return true;
        if (!string.IsNullOrEmpty(_newPassword)) return true;
        if (!string.IsNullOrEmpty(_confirmPassword)) return true;
        return false;
    }

    private void SelectPlan(PricingLevel level)
    {
        _selectedPlan = level.ToString();
        MarkDirty();
    }

    private async Task OnSectionSelectedAfter()
    {
        if (string.IsNullOrEmpty(_selectedSection)) return;
        await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('{_selectedSection}')?.scrollIntoView({{ behavior: 'smooth', block: 'start' }})");
    }

    private async Task SaveAll()
    {
        if (_loggedUser is null) return;

        _errors.Clear();
        _warnings.Clear();
        _info.Clear();

        var hasPasswordChange = !string.IsNullOrEmpty(_newPassword) || !string.IsNullOrEmpty(_confirmPassword) || !string.IsNullOrEmpty(_currentPassword);
        if (hasPasswordChange)
        {
            await ChangePasswordAsync();
        }

        if (_selectedPlan != _initialPlan)
        {
            await UpgradePricingPlan();
        }

        _initialDisplayName = _displayName;
        _isDirty = HasChanges();
    }

    private void DiscardChanges()
    {
        _displayName = _initialDisplayName;
        _selectedPlan = _initialPlan;
        _currentPassword = null;
        _newPassword = null;
        _confirmPassword = null;
        if (_passwordField is not null)
            _ = _passwordField.ResetAsync();
        _errors.Clear();
        _warnings.Clear();
        _info.Clear();
        _isDirty = false;
    }

    private string PasswordMatch(string arg)
    {
        if (!string.Equals(_newPassword, arg, StringComparison.Ordinal))
            return "Passwords don't match";

        return string.Empty;
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
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[A-Z]"))
            yield return "Password must contain at least one capital letter";
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[a-z]"))
            yield return "Password must contain at least one lowercase letter";
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[0-9]"))
            yield return "Password must contain at least one digit";
#endif
    }

    private async Task UpgradePricingPlan()
    {
        if (_loggedUser is null) return;

        var result = await UserService.UpdatePricingPlan(_loggedUser.UserId, (PricingLevel)Enum.Parse(typeof(PricingLevel), _selectedPlan));
        if (!result)
        {
            _errors.Insert(0, "Failed to change plan.");
            return;
        }

        _info.Insert(0, $"Successfully changed plan to {_selectedPlan}");
        _userData = await UserService.GetUser(_loggedUser.UserId);
        if (_userData is null) return;
        _initialPlan = _selectedPlan;

        try
        {
            _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
        }
        catch (Exception ex)
        {
            _errors.Insert(0, ex.Message);
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

        _errors.Clear();
        await LoginService.Logout();
        NavigationManager.NavigateTo("/");
    }

    private async Task ChangePasswordAsync()
    {
        if (_loggedUser is null) return;
        if (_passwordForm is null) return;
        if (_passwordField is null) return;
        if (string.IsNullOrEmpty(_confirmPassword)) return;

        await _passwordForm.Validate();
        if (!_passwordForm.IsValid) return;

        if (!string.Equals(_newPassword, _confirmPassword, StringComparison.Ordinal))
        {
            _warnings.Insert(0, "New passwords do not match.");
            return;
        }

        if (string.IsNullOrEmpty(_currentPassword))
        {
            _warnings.Insert(0, "Current password is required.");
            return;
        }

        var result = await UserService.UpdatePassword(_loggedUser.UserId, _confirmPassword, _currentPassword);
        if (!result)
        {
            _errors.Insert(0, "Failed to change password. Check that your current password is correct.");
            return;
        }

        _info.Insert(0, "Password changed successfully.");
        _currentPassword = null;
        _newPassword = null;
        _confirmPassword = null;
        await _passwordField.ResetAsync();
    }

    private Color GetStorageIndicatorColor()
    {
        if (_recordCapacity is not null && _recordCapacity.GetStorageUsedPercentage() >= 80)
            return Color.Error;
        return Color.Primary;
    }

    private sealed record PlanOption(PricingLevel Level, string DisplayName, string Price, IReadOnlyList<string> Features);
}