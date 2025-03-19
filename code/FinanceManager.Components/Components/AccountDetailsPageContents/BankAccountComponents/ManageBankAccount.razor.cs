
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents
{
    public partial class ManageBankAccount
    {
        private MudForm? form;
        private bool success;
        private string[] errors = { };
        private BankAccount? BankAccount { get; set; } = null;

        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

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

            BankAccount = FinancalAccountService.GetAccount<BankAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (BankAccount is null) return;

            AccountName = BankAccount.Name;
            AccountType = BankAccount.AccountType;
        }

        public async Task Update()
        {
            if (form is null) return;
            await form.Validate();

            if (!form.IsValid) return;
            if (BankAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                errors = [$"AccountName can not be empty"];
                return;
            }

            if (BankAccount is null) return;

            BankAccount updatedAccount = new BankAccount(BankAccount.UserId, BankAccount.AccountId, AccountName, AccountType);
            FinancalAccountService.UpdateAccount(updatedAccount);
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
                FinancalAccountService.RemoveAccount(AccountId);
                Navigation.NavigateTo($"");
                await AccountDataSynchronizationService.AccountChanged();
            }
        }
    }
}