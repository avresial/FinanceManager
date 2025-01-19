using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        public Dictionary<int, string> Accounts = [];
        public string ErrorMessage { get; set; } = string.Empty;

        protected override void OnInitialized()
        {
            try
            {
                UpdateAccounts();
                AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void AccountDataSynchronizationService_AccountsChanged()
        {
            UpdateAccounts();
            StateHasChanged();
        }

        private void UpdateAccounts()
        {
            try
            {
                Accounts.Clear();
                foreach (var account in FinancalAccountRepository.GetAvailableAccounts())
                {

                    var name = string.Empty;
                    if (account.Value == typeof(BankAccount))
                    {
                        var existinhAccount = FinancalAccountRepository.GetAccount<BankAccount>(account.Key, DateTime.UtcNow, DateTime.UtcNow);
                        if (existinhAccount is not null)
                            name = existinhAccount.Name;
                    }
                    else if (account.Value == typeof(StockAccount))
                    {
                        var existinhAccount = FinancalAccountRepository.GetAccount<StockAccount>(account.Key, DateTime.UtcNow, DateTime.UtcNow);
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
