using FinanceManager.Application.Services;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Account
{
    public partial class AddAccount : ComponentBase
    {
        private string _accountName { get; set; } = string.Empty;
        private string _selectedAccountType = string.Empty;
        private bool _success;
        private string[] _errors = { };

        private readonly string[] _accountTypes =
        {
            "Bank account", "Stock"
        };

        [Inject]
        public required IFinancalAccountRepository FinancalAccountRepository { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        public void Add()
        {
            var lastAccountId = FinancalAccountRepository.GetLastAccountId();

            switch (_selectedAccountType)
            {
                case "Bank account":
                    FinancalAccountRepository.AddAccount(new BankAccount(++lastAccountId, _accountName, Core.Enums.AccountType.Other));
                    break;

                case "Stock":
                    FinancalAccountRepository.AddAccount(new InvestmentAccount(++lastAccountId, _accountName));
                    break;
            }

            _accountName = string.Empty;
            _selectedAccountType = string.Empty;
            _ = AccountDataSynchronizationService.AccountChanged();

            StateHasChanged();
        }

        private IEnumerable<string> AccountNameValidation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                yield return "Account name is required!";
                yield break;
            }
            if (name.Length < 3)
                yield return "Account name must be at least of length 3";

        }
    }
}
