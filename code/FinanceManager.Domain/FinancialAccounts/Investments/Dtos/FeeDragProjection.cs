namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Portfolio value with and without known ETF expense ratios over a fixed horizon.
/// </summary>
public sealed class FeeDragProjection
{
    public int Years { get; set; }
    public decimal GrossFutureValue { get; set; }
    public decimal NetFutureValue { get; set; }
    public decimal CumulativeFeeCost { get; set; }
}