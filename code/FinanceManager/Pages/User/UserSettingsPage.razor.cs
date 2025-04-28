using FinanceManager.Domain.Enums;
using MudBlazor;
using System.Text.RegularExpressions;

namespace FinanceManager.WebUi.Pages.User;

public partial class UserSettingsPage
{
    PricingLevel currentUserPricingLevel = PricingLevel.Free;
    bool success;
    private MudForm _passwordForm;
    private string _requiredDeleteConfirmation = "delete my account";
    private string _deleteConfirmation;
    MudTextField<string> _passwordField;

    public string? ConfirmPassword { get; set; }

    private string _selectedPlan = $"{PricingLevel.Free}";
    private List<string> _plans = new() { $"{PricingLevel.Free}", $"{PricingLevel.Basic}", $"{PricingLevel.Premium}" };

    private int UsedStorage = @Random.Shared.Next(0, 100);
    private int TotalStorage = 100;

    private double StorageUsedPercentage => (double)UsedStorage / TotalStorage * 100;
    private string PasswordMatch(string arg)
    {
        if (_passwordField.Value != arg)
            return "Passwords don't match";
        return null;
    }

    private IEnumerable<string> PasswordStrength(string pw)
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

    private async Task ChangePasswordAsync()
    {
        await _passwordForm.Validate();
        if (_passwordForm.IsValid)
        {
            //if (_newPassword != _confirmPassword)
            //{
            //Snackbar.Add("New passwords do not match.", Severity.Error);
            //return;
            //}
            // Call service to change password
            //Snackbar.Add("Password changed successfully.", Severity.Success);
        }
    }

    private async Task ChangePlanAsync()
    {
        // Call service to update plan
        //Snackbar.Add($"Plan changed to {_selectedPlan}.", Severity.Success);
    }

    private Color GetStorageIndicatorColor()
    {
        if (StorageUsedPercentage >= 80)
            return Color.Error;
        return Color.Primary;
    }

    private async Task RemoveAccountAsync()
    {
        //bool confirmed = await DialogService.ShowMessageBox(
        //    "Delete Account",
        //    "Are you sure you want to permanently delete your account?",
        //    yesText: "Yes", cancelText: "Cancel");

        //if (confirmed)
        //{
        //    // Call service to remove account
        //    Snackbar.Add("Account deleted.", Severity.Error);
        //    NavigationManager.NavigateTo("/");
        //}
    }
}