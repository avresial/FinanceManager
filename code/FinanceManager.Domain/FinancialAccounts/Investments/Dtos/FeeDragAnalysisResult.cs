namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Read-only fee-drag analysis for the user's current ETF holdings.
/// Monetary values are expressed in the currency requested by the caller.
/// </summary>
public sealed class FeeDragAnalysisResult
{
    public decimal TotalHoldingsValue { get; set; }
    public decimal AnnualFeeCost { get; set; }
    public decimal WeightedExpenseRatio { get; set; }
    public decimal AssumedAnnualReturnRate { get; set; }
    public int MissingTerCount { get; set; }
    public decimal MissingTerHoldingsValue { get; set; }
    public int TotalHoldingsCount { get; set; }
    public List<FeeDragProjection> Projections { get; set; } = [];
    public List<FeeDragHolding> Holdings { get; set; } = [];
}