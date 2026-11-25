using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Transactions.Cash.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Transactions.Cash.Components.Crud;

public partial class RemoveCurrencyEntry
{
    private DateTime? _date = new DateTime();
    private decimal _balanceChange;

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string Title { get; set; }
    [Parameter] public required CashEntry CashEntry { get; set; }
    // The account's own currency (not the user's default), as shown on the transaction row.
    [Parameter] public required string Currency { get; set; }

    protected override void OnParametersSet()
    {
        _date = CashEntry.PostingDate;
        _balanceChange = CashEntry.ValueChange;
    }
}