using FinanceManager.Contracts.FinancialAccounts.Investments;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

/// <summary>The add/edit trade form in the account pages' dialog frame. The fields and the save flow live in <see cref="InvestmentTransactionFormBody"/>.</summary>
public partial class InvestmentTransactionForm : ComponentBase
{
    /// <summary>The investment account to trade in. Null saves through the owner routes (Transactions page).</summary>
    [Parameter] public int? AccountId { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public InvestmentTransactionDto? Transaction { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private string Title => Transaction is null ? "Add transaction" : "Edit transaction";

    private Task CloseAsync() => VisibleChanged.InvokeAsync(false);
}