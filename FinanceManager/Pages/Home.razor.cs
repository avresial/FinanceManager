using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public partial class Home : ComponentBase
    {
        [Inject]
        public required ILoginService LoginService { get; set; }

        [Inject]
        public required NavigationManager Navigation { get; set; }

        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (await LoginService.GetLoggedUser() is null)
                Navigation.NavigateTo("login");

            var availableAccounts = FinancalAccountRepository.GetAvailableAccounts();

            if (availableAccounts is null || availableAccounts.Count == 0)
                Navigation.NavigateTo("AddAccount");
        }
    }
}

