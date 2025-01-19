using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

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
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task UpdateAccounts()
        {
            try
            {
                var user = await loginService.GetLoggedUser();
                if (user is null) return;
                Accounts.Clear();
                foreach (var account in FinancalAccountRepository.GetAvailableAccounts())
                {

                    var name = string.Empty;
                    if (account.Value == typeof(BankAccount))
                    {
                        var existinhAccount = FinancalAccountRepository.GetAccount<BankAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
                        if (existinhAccount is not null)
                            name = existinhAccount.Name;
                    }
                    else if (account.Value == typeof(StockAccount))
                    {
                        var existinhAccount = FinancalAccountRepository.GetAccount<StockAccount>(user.UserId, account.Key, DateTime.UtcNow, DateTime.UtcNow);
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
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
