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

    private string KindIcon => Entry.Kind switch
    {
        TransactionKind.Investment => Icons.Material.Filled.ShowChart,
        TransactionKind.Bond => Icons.Material.Filled.Savings,
        _ => Icons.Material.Filled.AccountBalanceWallet,
    };
}