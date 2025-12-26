
using FinanceManager.Components.Components.SharedComponents;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.BankAccountComponents
{
    public partial class ManageBankAccount
    {
        private MudForm? _form;
        private bool _success;
        private string[] _errors = [];
        private BankAccount? BankAccount = null;

        public string AccountName { get; set; } = string.Empty;
        public AccountLabel AccountType { get; set; }

        [Parameter] public required int AccountId { get; set; }

        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required NavigationManager Navigation { get; set; }
        [Inject] public required IDialogService DialogService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }
        [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            BankAccount = await FinancalAccountService.GetAccount<BankAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (BankAccount is null) return;

            AccountName = BankAccount.Name;
            AccountType = BankAccount.AccountType;
        }

        public async Task Update()
        {
            if (_form is null) return;
            await _form.Validate();

            if (!_form.IsValid) return;
            if (BankAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (BankAccount is null) return;

            BankAccount updatedAccount = new BankAccount(BankAccount.UserId, BankAccount.AccountId, AccountName, AccountType);
            await FinancalAccountService.UpdateAccount(updatedAccount);
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
                await FinancalAccountService.RemoveAccount(AccountId);
                Navigation.NavigateTo($"");
                await AccountDataSynchronizationService.AccountChanged();
            }
        }
    }
}