using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout;

public partial class NavMenu : ComponentBase
{
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public ILogger<NavMenu> Logger { get; set; }

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

    private void AccountDataSynchronizationService_AccountsChanged()
    {
        _ = UpdateAccounts();
    }

    private async Task UpdateAccounts()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;
            Accounts.Clear();

            var test = await FinancialAccountService.GetAvailableAccounts();
            foreach (var account in test)
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

                Accounts.Add(account.Key, name);
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
