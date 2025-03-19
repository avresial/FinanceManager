using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages.Account
{
    public partial class AddAccount : ComponentBase
    {
        private string _accountName { get; set; } = string.Empty;
        private string _selectedAccountType = string.Empty;

        private int? _addedAccountId = null;
        private bool _success;
        private string[] _errors = { };

        private readonly string[] _accountTypes =
        {
            "Bank account", "Stock"
        };

        [Inject]
        public required ILogger<AddAccount> Logger { get; set; }

        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        public async Task Add()
        {
            int? lastAccountIdResult = null;
            try
            {
                lastAccountIdResult = FinancalAccountService.GetLastAccountId();
            }
            catch (Exception ex)
            {
                _errors = new[] { ex.Message };
                lastAccountIdResult = null;
                Logger.LogError(ex, "Error while adding bank account");
            }
            if (lastAccountIdResult is null) return;
            int lastAccountId = lastAccountIdResult.Value;
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            switch (_selectedAccountType)
            {
                case "Bank account":
                    try
                    {
                        FinancalAccountService.AddAccount(new BankAccount(user.UserId, ++lastAccountId, _accountName, Domain.Enums.AccountType.Other));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while adding bank account");
                    }
                    _addedAccountId = lastAccountId;
                    break;

                case "Stock":
                    try
                    {
                        FinancalAccountService.AddAccount(new StockAccount(user.UserId, ++lastAccountId, _accountName));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error while adding bank account");
                    }
                    _addedAccountId = lastAccountId;
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
