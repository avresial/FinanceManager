using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Crud;

public partial class RemoveCurrencyEntry
{
    private DateTime? _date = new DateTime();
    private decimal _balanceChange;
    private string _entryType = string.Empty;
    private Currency _currency = DefaultCurrency.PLN;

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string CurrencyAccountName { get; set; }
    [Parameter] public required CurrencyAccountEntry CurrencyAccountEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        _date = CurrencyAccountEntry.PostingDate;
        _balanceChange = CurrencyAccountEntry.ValueChange;
        _entryType = CurrencyAccountEntry.GetType().Name;
        _currency = SettingsService.GetCurrency();
    }
}