using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.BondAccountComponents.Crud;

public partial class RemoveBondEntry
{
    private DateTime? _date = new DateTime();
    private decimal _valueChange;
    private Currency _currency = DefaultCurrency.PLN;

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string BondAccountName { get; set; }
    [Parameter] public required BondAccountEntry BondAccountEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        _date = BondAccountEntry.PostingDate;
        _valueChange = BondAccountEntry.ValueChange;
        _currency = SettingsService.GetCurrency();
    }
}