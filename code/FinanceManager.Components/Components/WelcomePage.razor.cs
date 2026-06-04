using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components;

public partial class WelcomePage : ComponentBase
{
    private const string _guestLogin = "Guest";
    private bool _isLaunchingDemo;

    [Inject] public required ILogger<WelcomePage> Logger { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

    private void AddAccount(string type) => Navigation.NavigateTo($"AddAccount?type={type}");

    private async Task ExploreDemo()
    {
        if (_isLaunchingDemo) return;
        _isLaunchingDemo = true;

        try
        {
            FinancialAccountService.InitializeMock();
            await LoginService.Login(_guestLogin, "GuestPassword");
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo("");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while launching the demo account");
            _isLaunchingDemo = false;
        }
    }
}