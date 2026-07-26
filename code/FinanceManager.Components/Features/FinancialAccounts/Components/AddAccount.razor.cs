using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.FinancialAccounts.Components;

public partial class AddAccount : ComponentBase
{
    private string _accountName = string.Empty;
    private string _selectedAccountType = "Currency account";

    private int? _addedAccountId = null;
    private bool _success;
    private string[] _errors = { };

    private readonly string[] _accountTypes =
    {
        "Currency account", "Stock account", "Bond account"
    };

    // Lets the welcome screen deep-link straight to a preselected account type (e.g. AddAccount?type=Stock).
    [Parameter, SupplyParameterFromQuery] public string? Type { get; set; }

    protected override void OnInitialized()
    {
        if (string.IsNullOrWhiteSpace(Type)) return;

        var match = Array.Find(_accountTypes, t => t.StartsWith(Type, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            _selectedAccountType = match;
    }

    [Inject] public required ILogger<AddAccount> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required CurrencyAccountHttpClient CurrencyAccountHttpClient { get; set; }
    [Inject] public required InvestmentAccountHttpClient InvestmentAccountHttpClient { get; set; }
    [Inject] public required BondAccountHttpClient BondAccountHttpClient { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    public async Task Add()
    {
        try
        {
            switch (_selectedAccountType)
            {
                case "Currency account":
                    _addedAccountId = await CurrencyAccountHttpClient.AddAccountAsync(new Domain.FinancialAccounts.Shared.Commands.AddAccount(_accountName));
                    break;

                case "Stock account":
                    _addedAccountId = await InvestmentAccountHttpClient.AddAccountAsync(new Domain.FinancialAccounts.Shared.Commands.AddAccount(_accountName));
                    break;

                case "Bond account":
                    _addedAccountId = await BondAccountHttpClient.AddAccountAsync(new Domain.FinancialAccounts.Shared.Commands.AddAccount(_accountName));
                    break;
            }
        }
        catch (Exception ex)
        {
            _addedAccountId = null;
            _errors = [ex.Message];
            Logger.LogError(ex, "Error while adding currency account");
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