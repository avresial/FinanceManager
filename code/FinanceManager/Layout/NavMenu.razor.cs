using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

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
                var user = await loginService.GetLoggedUser();
                if (user is null) return;
                Accounts.Clear();

                var test = await FinancalAccountService.GetAvailableAccounts();
                foreach (var account in test)
                {

                    var name = string.Empty;
                    if (account.Value == typeof(BankAccount))
                    {
                        var existinhAccount = await FinancalAccountService.GetAccount<BankAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                        if (existinhAccount is not null)
                            name = existinhAccount.Name;
                    }
                    else if (account.Value == typeof(StockAccount))
                    {
                        var existinhAccount = await FinancalAccountService.GetAccount<StockAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                        if (existinhAccount is not null)
                            name = existinhAccount.Name;
                    }
                    else
                    {
                        Console.WriteLine($"Error - type can not be handled, Account id {account.Key}");
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
}
