namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>A trade quantity change, including trades with no principal value.</summary>
public sealed record PortfolioQuantityChange(
    long SourceId,
    int AccountId,
    long InstrumentId,
    DateTime Date,
    PortfolioMovementKind Kind,
    decimal Quantity);