using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Transactions.Bonds.Components.Crud;

public partial class RemoveBondEntry
{
    private DateTime? _date = new DateTime();
    private decimal _valueChange;
    private Currency _currency = DefaultCurrency.PLN;

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string Title { get; set; }
    [Parameter] public required BondEntry BondEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        _date = BondEntry.PostingDate;
        _valueChange = BondEntry.ValueChange;
        _currency = SettingsService.GetCurrency();
    }
}