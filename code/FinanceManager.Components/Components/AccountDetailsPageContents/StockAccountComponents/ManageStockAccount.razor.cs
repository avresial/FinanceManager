using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents
{
    public partial class ManageStockAccount : ComponentBase
    {
        private MudForm? form;
        private bool success;
        private string[] errors = { };

        private StockAccount? InvestmentAccount { get; set; } = null;

        public string AccountName { get; set; } = string.Empty;

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

        [Inject]
        public required NavigationManager Navigation { get; set; }

        [Inject]
        public required IDialogService DialogService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            InvestmentAccount = FinancalAccountRepository.GetAccount<StockAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (InvestmentAccount is null) return;

            AccountName = InvestmentAccount.Name;
        }

        public async Task Update()
        {
            if (form is null) return;
            await form.Validate();

            if (!form.IsValid) return;
            if (InvestmentAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                errors = [$"AccountName can not be empty"];
                return;
            }

            if (InvestmentAccount is null) return;

            StockAccount updatedAccount = new StockAccount(InvestmentAccount.UserId, InvestmentAccount.AccountId, AccountName);
            FinancalAccountRepository.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }

        public async Task Remove()
        {
            var options = new DialogOptions { CloseOnEscapeKey = true };
            var dialog = await DialogService.ShowAsync<ConfirmRemoveDialog>("Simple Dialog", options);
            var result = await dialog.Result;

            if (result is not null && !result.Canceled)
            {
                FinancalAccountRepository.RemoveAccount(AccountId);
                Navigation.NavigateTo($"");
                await AccountDataSynchronizationService.AccountChanged();
            }
        }
    }
}