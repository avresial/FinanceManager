namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public record AssetClassHoldings(string AssetClass, IReadOnlyList<string> Holdings);

public record DiversificationBreakdown(IReadOnlyList<AssetClassHoldings> AssetClasses);