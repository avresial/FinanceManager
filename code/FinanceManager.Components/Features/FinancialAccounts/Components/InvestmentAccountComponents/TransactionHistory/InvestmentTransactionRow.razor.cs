using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents.TransactionHistory;

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

    // The overview line leads with unrealised performance for priced purchases — what the position is
    // worth now versus what it cost — because that answers "how is this doing?" more naturally than the
    // purchase outlay. Sells and not-yet-priced buys have no gain/loss figure, so they fall back to the
    // trade's cash impact, which is the only amount available for them.
    private bool ShowGainLoss => Transaction.Type == InvestmentTransactionType.Buy && Valuation is { HasCurrentPrice: true };
    private decimal PrimaryAmount => ShowGainLoss ? Valuation!.GainLoss : CashImpact;
    private string PrimaryCurrency => ShowGainLoss ? Valuation!.Currency : Transaction.Currency;

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private Task OnEditClicked() => OnEdit.InvokeAsync(Transaction);

    private Task OnDeleteClicked() => OnDelete.InvokeAsync(Transaction);
}