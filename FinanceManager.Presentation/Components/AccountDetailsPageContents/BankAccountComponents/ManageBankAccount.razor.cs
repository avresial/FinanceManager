using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Presentation.Components.AccountDetailsPageContents.BankAccountComponents
{
    public partial class ManageBankAccount
    {
        private MudForm form;
        private bool success;
        private string[] errors = { };

        private BankAccount? BankAccount { get; set; } = null;

        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IAccountService AccountService { get; set; }

        [Inject]
        public NavigationManager Navigation { get; set; }

        protected override void OnParametersSet()
        {
            BankAccount = AccountService.GetAccount<BankAccount>(AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (BankAccount is null) return;

            AccountName = BankAccount.Name;
            AccountType = BankAccount.AccountType;
        }


        public async Task Update()
        {
            await form.Validate();
            if (!form.IsValid) return;
            if (BankAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                errors = new string[] { $"AccountName can not be empty" };
                return;
            }

            if (BankAccount is null) return;

            BankAccount updatedAccount = new BankAccount(BankAccount.Id, AccountName, AccountType);
            AccountService.UpdateAccount(updatedAccount);
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }

        public async Task Remove()
        {
        }
    }
}