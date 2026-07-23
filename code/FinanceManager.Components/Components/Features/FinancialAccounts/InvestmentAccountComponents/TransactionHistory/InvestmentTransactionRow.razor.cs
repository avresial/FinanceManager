using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents.TransactionHistory;

public partial class InvestmentTransactionRow
{
    [Parameter] public required InvestmentTransactionDto Transaction { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    /// <summary>Valuation/performance figures for this transaction (Buy only); null when not available.</summary>
    [Parameter] public InvestmentTransactionValuationDto? Valuation { get; set; }
    [Parameter] public EventCallback<InvestmentTransactionDto> OnEdit { get; set; }
    [Parameter] public EventCallback<InvestmentTransactionDto> OnDelete { get; set; }

    private bool _expanded;

    private string TypeLabel => Transaction.Type == InvestmentTransactionType.Sell ? "Sell" : "Buy";
    private Color TypeColor => Transaction.Type == InvestmentTransactionType.Sell ? Color.Error : Color.Success;

    private decimal GrossValue => Transaction.Quantity * Transaction.UnitPrice;

    // Performance tiles apply to purchases only, and only once the server has priced the position.
    private bool ShowValuation => Transaction.Type == InvestmentTransactionType.Buy && Valuation is not null;

    // Buy is a cash outflow (negative), sell is a cash inflow (positive). Fees always reduce the cash impact.
    private decimal CashImpact => Transaction.Type == InvestmentTransactionType.Sell
        ? GrossValue - (Transaction.Fee ?? 0m)
        : -(GrossValue + (Transaction.Fee ?? 0m));

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private Task OnEditClicked() => OnEdit.InvokeAsync(Transaction);

    private Task OnDeleteClicked() => OnDelete.InvokeAsync(Transaction);
}