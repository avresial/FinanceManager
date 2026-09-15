namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Valued ETF holding included in a fee-drag analysis.
/// </summary>
public sealed class FeeDragHolding
{
    public long ListingId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? ExpenseRatio { get; set; }
    public decimal AnnualFeeCost { get; set; }
}