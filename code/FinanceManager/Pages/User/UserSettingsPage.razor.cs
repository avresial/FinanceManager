using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text.RegularExpressions;

namespace FinanceManager.WebUi.Pages.User;

public partial class UserSettingsPage
{
    private const string _requiredDeleteConfirmation = "delete my account";

    private UserSession? _loggedUser;
    private Domain.Entities.Login.User? _userData;

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _infos = [];

    private bool _success;
    private string _selectedPlan = $"{PricingLevel.Free}";
    private string? _confirmPassword { get; set; }
    private string? _deleteConfirmation;
    private MudForm? _passwordForm;
    private MudTextField<string>? _passwordField;
    private List<string> _plans = new() { $"{PricingLevel.Free}", $"{PricingLevel.Basic}", $"{PricingLevel.Premium}" };

    private RecordCapacity? _recordCapacity;


    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }




    protected override async Task OnInitializedAsync()
    {
        _loggedUser = await LoginService.GetLoggedUser();
        if (_loggedUser is null) return;

        _userData = await UserService.GetUser(_loggedUser.UserId);
        if (_userData is null) return;

        if (_plans.Contains(_userData.PricingLevel.ToString()))
            _selectedPlan = _userData.PricingLevel.ToString();

        try
        {
            _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
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
        yield break;
        if (pw.Length < 8)
            yield return "Password must be at least of length 8";
        if (!Regex.IsMatch(pw, @"[A-Z]"))
            yield return "Password must contain at least one capital letter";
        if (!Regex.IsMatch(pw, @"[a-z]"))
            yield return "Password must contain at least one lowercase letter";
        if (!Regex.IsMatch(pw, @"[0-9]"))
            yield return "Password must contain at least one digit";
    }
    private async Task UpgradePricingPlan()
    {
        if (_loggedUser is null) return;

        var result = await UserService.UpdatePricingPlan(_loggedUser.UserId, (PricingLevel)Enum.Parse(typeof(PricingLevel), _selectedPlan));
        _errors.Clear();
        if (!result) _errors.Add("Failed to change plan.");

    }

    private async Task DeleteMyAccount()
    {
        if (_loggedUser is null) return;
        var result = await UserService.RemoveUser(_loggedUser.UserId);
        if (!result)
        {
            _errors.Add("Failed to remove user.");
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
        if (_passwordField is null) return;

        await _passwordForm.Validate();
        if (_passwordForm.IsValid)
        {
            if (_passwordField.Value != _confirmPassword)
            {
                _warnings.Add("New passwords do not match.");
                return;
            }
            else
            {
                var result = await UserService.UpdatePricingPlan(_loggedUser.UserId, (PricingLevel)Enum.Parse(typeof(PricingLevel), _selectedPlan));
                if (!result)
                {
                    _errors.Add("Failed to change password.");
                    return;
                }
                else
                {
                    _errors.Clear();
                    _infos.Add("Password changed successfully.");
                }
            }
        }
    }
    private Color GetStorageIndicatorColor()
    {
        if (GetStorageUsedPercentage() >= 80)
            return Color.Error;
        return Color.Primary;
    }
    private double GetStorageUsedPercentage()
    {
        if (_recordCapacity is null) return -1;
        return (double)_recordCapacity.UsedCapacity / _recordCapacity.TotalCapacity * 100;
    }
}