using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class TransactionHistoryRow : ComponentBase
{
    [Parameter, EditorRequired] public required TransactionHistoryItem Item { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    /// <summary>Cash rows: true when the other cash rows of the day share this time, so it is not repeated.</summary>
    [Parameter] public bool SiblingsShareSameTime { get; set; }

    /// <summary>Bond rows: the bond definition, once loaded, for the valuation details.</summary>
    [Parameter] public BondDetails? BondDetails { get; set; }

    /// <summary>Investment rows: the trade's valuation, once loaded.</summary>
    [Parameter] public InvestmentTransactionValuationDto? Valuation { get; set; }

    [Parameter] public EventCallback<InvestmentTransactionDto> OnEditInvestment { get; set; }
    [Parameter] public EventCallback<InvestmentTransactionDto> OnDeleteInvestment { get; set; }
}