using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class TransactionLogRow : ComponentBase
{
    [Parameter, EditorRequired] public required TransactionLogEntryDto Entry { get; set; }

    /// <summary>Show the entry date on the second line; the Transactions page hides it because its day headers carry it.</summary>
    [Parameter] public bool ShowDate { get; set; } = true;

    // Currency entries can be highlighted on the account page via ?entryId; bond and investment
    // entry IDs are not addressable there, so those rows open the plain account page.
    internal string Href => Entry.AccountType == AccountType.Currency
        ? $"/AccountDetails/{Entry.AccountId}?entryId={Entry.EntryId}"
        : $"/AccountDetails/{Entry.AccountId}";

    private string AccountTypeIcon => Entry.AccountType switch
    {
        AccountType.Stock => Icons.Material.Filled.ShowChart,
        AccountType.Bond => Icons.Material.Filled.Savings,
        _ => Icons.Material.Filled.AccountBalanceWallet,
    };
}