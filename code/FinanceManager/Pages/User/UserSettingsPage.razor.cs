using FinanceManager.Domain.Enums;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.User;
public partial class UserSettingsPage
{
    private MudForm _passwordForm;
    private string _currentPassword;
    private string _newPassword;
    private string _confirmPassword;

    private string _selectedPlan = $"{PricingLevel.Free}";
    private List<string> _plans = new() { $"{PricingLevel.Free}", $"{PricingLevel.Basic}", $"{PricingLevel.Premium}" };

    private int UsedStorage = 5; // In GB
    private int TotalStorage = 10; // In GB

    private double StorageUsedPercentage => (double)UsedStorage / TotalStorage * 100;

    private async Task ChangePasswordAsync()
    {
        await _passwordForm.Validate();
        if (_passwordForm.IsValid)
        {
            if (_newPassword != _confirmPassword)
            {
                //Snackbar.Add("New passwords do not match.", Severity.Error);
                return;
            }
            // Call service to change password
            //Snackbar.Add("Password changed successfully.", Severity.Success);
        }
    }

    private async Task ChangePlanAsync()
    {
        // Call service to update plan
        //Snackbar.Add($"Plan changed to {_selectedPlan}.", Severity.Success);
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