using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public class AccountDetailsBase : ComponentBase
    {
        [Parameter]
        public string AccountName { get; set; }

        [Inject]
        public AccountsService AccountsService { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public IEnumerable<AccountEntry> Entries { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                if (AccountsService.Accounts.ContainsKey(AccountName))
                    Entries = AccountsService.Accounts[AccountName].Take(500);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
