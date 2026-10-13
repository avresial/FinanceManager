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

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string CurrencyAccountName { get; set; }
    [Parameter] public required CurrencyAccountEntry CurrencyAccountEntry { get; set; }
    // The account's own currency (not the user's default), as shown on the transaction row.
    [Parameter] public required string Currency { get; set; }

    protected override void OnParametersSet()
    {
        _date = CurrencyAccountEntry.PostingDate;
        _balanceChange = CurrencyAccountEntry.ValueChange;
    }
}