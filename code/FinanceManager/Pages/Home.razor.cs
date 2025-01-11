using FinanceManager.Application.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
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
        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var loggedUser = await LoginService.GetLoggedUser();

            if (loggedUser is null)
            {
                Navigation.NavigateTo("login");
                return;
            }

            var availableAccounts = FinancalAccountRepository.GetAvailableAccounts();
            if ((availableAccounts is null || availableAccounts.Count == 0) && loggedUser.UserName.ToLower() == "guest")
            {
                FinancalAccountRepository.InitializeMock();
                availableAccounts = FinancalAccountRepository.GetAvailableAccounts();
                await AccountDataSynchronizationService.AccountChanged();
            }

            if (availableAccounts is null || availableAccounts.Count == 0)
                Navigation.NavigateTo("AddAccount");
        }
    }
}

