namespace FinanceManager.Domain.Insights.Entities;

public record AssetClassHoldings(string AssetClass, IReadOnlyList<string> Holdings);

public record DiversificationBreakdown(IReadOnlyList<AssetClassHoldings> AssetClasses);