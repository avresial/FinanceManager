using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages.Account;

public partial class AddAccount : ComponentBase
{
    private string _accountName { get; set; } = string.Empty;
    private string _selectedAccountType = "Bank account";

    private int? _addedAccountId = null;
    private bool _success;
    private string[] _errors = { };

    private readonly string[] _accountTypes =
    {
        "Bank account", "Stock account"
    };

    [Inject] public required ILogger<AddAccount> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required BankAccountHttpClient BankAccountHttpClient { get; set; }
    [Inject] public required StockAccountHttpClient StockAccountHttpClient { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    public async Task Add()
    {
        try
        {
            switch (_selectedAccountType)
            {
                case "Bank account":
                    _addedAccountId = await BankAccountHttpClient.AddAccountAsync(new Application.Commands.Account.AddAccount(_accountName));
                    break;

                case "Stock account":
                    _addedAccountId = await StockAccountHttpClient.AddAccountAsync(new Application.Commands.Account.AddAccount(_accountName));
                    break;
            }
        }
        catch (Exception ex)
        {
            _addedAccountId = null;
            _errors = [ex.Message];
            Logger.LogError(ex, "Error while adding bank account");
        }


        if (_errors.Length == 0)
        {
            _accountName = string.Empty;
            _selectedAccountType = string.Empty;
            await AccountDataSynchronizationService.AccountChanged();
        }

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