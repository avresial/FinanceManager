using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout;

public partial class NavMenu : ComponentBase
{
    private bool collapseNavMenu = true;
    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;
    private bool displayAssetsLink = false;
    private bool displayLiabilitiesLink = false;

    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<NavMenu> Logger { get; set; }


    public Dictionary<int, string> Accounts = [];
    public string ErrorMessage { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await UpdateAccounts();
            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }
    private async void AccountDataSynchronizationService_AccountsChanged()
    {
        await UpdateAccounts();
    }
    private async Task UpdateAccounts()
    {
        UserSession? user = null;

        try
        {
            user = await LoginService.GetLoggedUser();
            if (user is null) return;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        try
        {
            Accounts.Clear();

            var availableAccounts = await FinancialAccountService.GetAvailableAccounts();
            foreach (var account in availableAccounts)
            {
                var name = string.Empty;
                if (account.Value == typeof(BankAccount))
                {
                    var existingAccount = await FinancialAccountService.GetAccount<BankAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                    if (existingAccount is not null)
                        name = existingAccount.Name;
                }
                else if (account.Value == typeof(StockAccount))
                {
                    var existingAccount = await FinancialAccountService.GetAccount<StockAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                    if (existingAccount is not null)
                        name = existingAccount.Name;
                }
                else
                {
                    Logger.LogError($"account type {account.Value.Name} can not be handled, Account id {account.Key}");
                    continue;
                }

                if (!Accounts.ContainsKey(account.Key))
                    Accounts.Add(account.Key, name);
            }


        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while getting available accounts");
        }
        try
        {
            displayAssetsLink = await MoneyFlowService.IsAnyAccountWithAssets(user.UserId);
        }
        catch (HttpRequestException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while checking if any account with assets");
            ErrorMessage = ex.Message;
        }
        try
        {
            displayLiabilitiesLink = await MoneyFlowService.IsAnyAccountWithLiabilities(user.UserId);
        }
        catch (HttpRequestException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while checking if any account with liabilities");
            ErrorMessage = ex.Message;
        }

        await InvokeAsync(StateHasChanged);
    }
}
