using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout;

public partial class NavMenu : ComponentBase
{
    private bool _displayAssetsLink = false;
    private bool _displayLiabilitiesLink = false;

    [Parameter] public bool DrawerIsOpen { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
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

    private async void AccountDataSynchronizationService_AccountsChanged() => await UpdateAccounts();
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
                else if (account.Value == typeof(BondAccount))
                {
                    var existingAccount = await FinancialAccountService.GetAccount<BondAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                    if (existingAccount is not null)
                        name = existingAccount.Name;
                }
                else
                {
                    Logger.LogError("account type {account.Name} can not be handled, Account id {account.Key}", account.Value.Name, account.Key);
                    continue;
                }

                Accounts.TryAdd(account.Key, name);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while getting available accounts");
        }

        try
        {
            _displayAssetsLink = await AssetsHttpClient.IsAnyAccountWithAssets(user.UserId);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while checking if any account with assets");
            ErrorMessage = ex.Message;
        }

        try
        {
            _displayLiabilitiesLink = await LiabilitiesHttpClient.IsAnyAccountWithLiabilities(user.UserId);
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