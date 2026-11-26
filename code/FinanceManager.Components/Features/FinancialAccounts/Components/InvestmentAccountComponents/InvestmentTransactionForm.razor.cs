using FinanceManager.Contracts.FinancialAccounts.Investments;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

/// <summary>The add/edit trade form in a dialog frame. The fields and the save flow live in <see cref="InvestmentTransactionFormBody"/>.</summary>
public partial class InvestmentTransactionForm : ComponentBase
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public InvestmentTransactionDto? Transaction { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private string Title => Transaction is null ? "Add transaction" : "Edit transaction";

    private Task CloseAsync() => VisibleChanged.InvokeAsync(false);
}