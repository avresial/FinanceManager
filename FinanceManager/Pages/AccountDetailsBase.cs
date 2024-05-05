using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public class AccountDetailsBase : ComponentBase
    {
        private const int maxTableSize = 500;

        [Parameter]
        public string AccountName { get; set; }

        [Inject]
        public AccountsService AccountsService { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public IEnumerable<AccountEntry> Entries { get; set; }

        protected override async Task OnInitializedAsync()
        {
            UpdateEntries();
        }

        protected override async Task OnParametersSetAsync()
        {
            UpdateEntries();
        }

        private void UpdateEntries()
        {
            try
            {
                if (AccountsService.Contains(AccountName))
                    Entries = AccountsService.Get(AccountName).Take(maxTableSize);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
