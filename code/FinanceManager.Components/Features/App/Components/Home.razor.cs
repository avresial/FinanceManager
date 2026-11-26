using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
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
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }


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

            // Legacy accounts are already cached by the account service, so they are checked first and the transaction log is
            // only asked when there are none. Transactions without any account (the guest demo, or entries added on the
            // Transactions page) are enough to show the dashboard.
            _showWelcome = (await FinancialAccountService.GetAvailableAccounts()).Count == 0
                && (await TransactionLogHttpClient.GetLastTransactions(loggedUser.UserId, 1)).Count == 0;
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