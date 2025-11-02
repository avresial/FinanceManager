using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class EditUserPage
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private Domain.Entities.Login.User? _userData;

    private bool _isLoadingPage;
    private bool _success;
    private string _selectedPlan = $"{PricingLevel.Free}";
    private string? _selectedUserRole;
    private string? _confirmPassword { get; set; }
    private MudForm? _passwordForm;
    private MudTextField<string>? _passwordField;
    private List<string> _plans = [$"{PricingLevel.Free}", $"{PricingLevel.Basic}", $"{PricingLevel.Premium}"];
    private List<string> _roles = [$"{UserRole.User}", $"{UserRole.Admin}"];
    private RecordCapacity? _recordCapacity;

    [Parameter] public required int UserId { get; set; }

    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<EditUserPage> Logger { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoadingPage = true;

        try
        {
            _userData = await UserService.GetUser(UserId);
            _selectedUserRole = _userData?.UserRole.ToString() ?? $"{UserRole.User}";
            _selectedPlan = _userData?.PricingLevel.ToString() ?? $"{PricingLevel.Free}";
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to load user data: {ex.Message}");
            // _logger.LogError(ex, "Error loading user data");
        }

        _isLoadingPage = false;
    }

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

    private async Task ChangeUserRole()
    {
        if (_userData is null) return;
        if (string.IsNullOrEmpty(_selectedUserRole)) return;

        try
        {

            var result = await UserService.UpdateRole(_userData.UserId, (UserRole)Enum.Parse(typeof(UserRole), _selectedUserRole));
            if (!result)
                _errors.Insert(0, "Failed to change role.");
            else
                _info.Insert(0, $"Successfully changed role");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            _errors.Insert(0, "Failed to change role.");
            return;
        }
    }
    private async Task UpgradePricingPlan()
    {
        if (_userData is null) return;

        bool result = false;
        try
        {
            result = await UserService.UpdatePricingPlan(_userData.UserId, (PricingLevel)Enum.Parse(typeof(PricingLevel), _selectedPlan));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            _errors.Insert(0, "Failed to change plan.");
            return;
        }

        _errors.Clear();
        if (!result)
        {
            _errors.Insert(0, "Failed to change plan.");
        }
        else
        {
            _info.Insert(0, $"Successfully upgraded plan to {_selectedPlan}");

            _userData = await UserService.GetUser(_userData.UserId);
            if (_userData is null) return;

            try
            {
                _recordCapacity = await UserService.GetRecordCapacity(_userData.UserId);
            }
            catch (Exception ex)
            {
                _errors.Insert(0, ex.Message);
            }
        }

    }
    private async Task ChangePasswordAsync()
    {
        if (_userData is null) return;
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
                var result = await UserService.UpdatePassword(_userData.UserId, _confirmPassword);
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
}