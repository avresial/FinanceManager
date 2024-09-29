using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
    public class NavMenuBase : ComponentBase
    {
        [Inject]
        public IAccountService AccountsService { get; set; }

        public List<string> AccountNames = new List<string>();

        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                AccountNames = [.. AccountsService.GetAccounts<BankAccount>(DateTime.Now.AddDays(-31), DateTime.Now).Select(x => x.Name).OrderBy(x => x)];
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
                AccountNames = [.. AccountsService.GetAccounts<BankAccount>(DateTime.Now.AddDays(-31), DateTime.Now).Select(x => x.Name).OrderBy(x => x)];
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            StateHasChanged();
        }
    }
}
