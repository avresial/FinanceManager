using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public partial class Import : ComponentBase
    {
        public ElementReference MyElementReference;

        public Type? accountType = null;

        [Parameter]
        public required string AccountName { get; set; }

        [Inject]
        public required IFinancalAccountRepository BankAccountRepository { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;


        protected override void OnInitialized()
        {
            UpdateEntries();
        }

        protected override void OnParametersSet()
        {
            MyElementReference = default;
            accountType = null;
            UpdateEntries();
        }
        private void UpdateEntries()
        {
            try
            {
                var accounts = BankAccountRepository.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountName))
                    accountType = accounts[AccountName];

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
