using FinanceManager.Application.Services;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        public Dictionary<int, string> Accounts = new Dictionary<int, string>();

        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
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
                    else if (account.Value == typeof(InvestmentAccount))
                    {
                        var existinhAccount = FinancalAccountRepository.GetAccount<InvestmentAccount>(account.Key, DateTime.UtcNow, DateTime.UtcNow);
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
