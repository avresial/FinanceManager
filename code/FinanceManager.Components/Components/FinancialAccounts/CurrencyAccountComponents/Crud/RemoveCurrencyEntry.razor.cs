using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents.Crud;

public partial class RemoveCurrencyEntry
{
    private DateTime? date = new DateTime();
    private decimal BalanceChange;
    private string _entryType = string.Empty;
    private Currency _currency = DefaultCurrency.PLN;

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string CurrencyAccountName { get; set; }
    [Parameter] public required CurrencyAccountEntry CurrencyAccountEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        date = CurrencyAccountEntry.PostingDate;
        BalanceChange = CurrencyAccountEntry.ValueChange;
        _entryType = CurrencyAccountEntry.GetType().Name;
        _currency = SettingsService.GetCurrency();
    }
}
