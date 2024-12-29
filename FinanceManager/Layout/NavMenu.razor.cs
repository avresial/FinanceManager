using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IAccountService AccountsService { get; set; }

        public Dictionary<int, string> Accounts = new Dictionary<int, string>();

        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                UpdateAccounts();
                AccountsService.AccountsChanged += AccountsService_AccountsChanged;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void AccountsService_AccountsChanged()
        {
            UpdateAccounts();
            StateHasChanged();
        }

        private void UpdateAccounts()
        {
            try
            {
                Accounts.Clear();
                foreach (var account in AccountsService.GetAvailableAccounts())
                {

                    var name = string.Empty;
                    if (account.Value == typeof(BankAccount))
                    {
                        var existinhAccount = AccountsService.GetAccount<BankAccount>(account.Key, DateTime.UtcNow, DateTime.UtcNow);
                        if (existinhAccount is not null)
                            name = existinhAccount.Name;
                    }
                    else if (account.Value == typeof(InvestmentAccount))
                    {
                        var existinhAccount = AccountsService.GetAccount<InvestmentAccount>(account.Key, DateTime.UtcNow, DateTime.UtcNow);
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
