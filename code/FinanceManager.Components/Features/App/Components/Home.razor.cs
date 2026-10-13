using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.App.Components;

public partial class Home : ComponentBase
{
    private bool _isLoading;
    private bool _showWelcome;
    private bool _hasAccountLoadingError;

    [Inject] public required ILogger<Home> Logger { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }


    protected override Task OnInitializedAsync() => LoadAccountsAsync();

    private async Task LoadAccountsAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _hasAccountLoadingError = false;
        try
        {
            var loggedUser = await LoginService.GetLoggedUser();
            if (loggedUser is null)
            {
                Navigation.NavigateTo("login");
                return;
            }

            var availableAccounts = await FinancialAccountService.GetAvailableAccounts();
            if (availableAccounts.Count == 0 && loggedUser.UserName.ToLower() == "guest")
            {
                FinancialAccountService.InitializeMock();
                availableAccounts = await FinancialAccountService.GetAvailableAccounts();
                await AccountDataSynchronizationService.AccountChanged();
            }

            _showWelcome = availableAccounts.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load accounts");
            _hasAccountLoadingError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}