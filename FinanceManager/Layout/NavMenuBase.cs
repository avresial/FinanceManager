using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
    public class NavMenuBase : ComponentBase
    {
        [Inject]
        public AccountsService AccountsService { get; set; }

        public List<string> AccountNames = new List<string>();

        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            AccountsService.AccountsChanged += AccountsService_AccountsChanged;
            try
            {
                AccountNames = [.. AccountsService.Get().Select(x=>x.Name).OrderBy(x => x)];
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void AccountsService_AccountsChanged(string arg1)
        {
            try
            {
                AccountNames = [.. AccountsService.Get().Select(x => x.Name).OrderBy(x => x)];
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            StateHasChanged();
        }
    }
}
