using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
    public partial class NavMenu : ComponentBase
    {
        [Inject]
        public required IAccountService AccountsService { get; set; }

        public List<string> AccountNames = new List<string>();

        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                AccountNames = AccountsService.GetAvailableAccounts().Keys.ToList();
                AccountsService.AccountsChanged += AccountsService_AccountsChanged;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void AccountsService_AccountsChanged()
        {
            try
            {
                AccountNames = AccountsService.GetAvailableAccounts().Keys.ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            StateHasChanged();
        }
    }
}
