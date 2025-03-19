using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages.Account
{
    public partial class Import : ComponentBase
    {
        public ElementReference MyElementReference;

        public Type? accountType = null;

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

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
                var accounts = FinancalAccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountId))
                    accountType = accounts[AccountId];

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
