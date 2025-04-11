using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages.Account;

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

    [Inject] public required ILogger<AddAccount> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    public async Task Add()
    {
        int? lastAccountIdResult = null;
        try
        {
            lastAccountIdResult = await FinancalAccountService.GetLastAccountId();
        }
        catch (Exception ex)
        {
            _errors = [ex.Message];
            lastAccountIdResult = null;
            Logger.LogError(ex, "Error while adding bank account");
        }
        if (lastAccountIdResult is null) return;
        int lastAccountId = lastAccountIdResult.Value;
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        switch (_selectedAccountType)
        {
            case "Bank account":
                try
                {
                    await FinancalAccountService.AddAccount(new BankAccount(user.UserId, ++lastAccountId, _accountName, Domain.Enums.AccountType.Other));
                    _addedAccountId = lastAccountId;
                }
                catch (Exception ex)
                {
                    _errors = [ex.Message];
                    Logger.LogError(ex, "Error while adding bank account");
                }
                break;

            case "Stock":
                try
                {
                    await FinancalAccountService.AddAccount(new StockAccount(user.UserId, ++lastAccountId, _accountName));
                    _addedAccountId = lastAccountId;
                }
                catch (Exception ex)
                {
                    _errors = [ex.Message];
                    Logger.LogError(ex, "Error while adding bank account");
                }
                break;
        }

        _accountName = string.Empty;
        _selectedAccountType = string.Empty;
        await AccountDataSynchronizationService.AccountChanged();

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
