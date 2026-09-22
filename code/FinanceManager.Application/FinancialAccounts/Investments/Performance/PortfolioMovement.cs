namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>A native-currency cash movement; direction is expressed by <see cref="Kind"/>.</summary>
public sealed record PortfolioMovement(
    long SourceId,
    int AccountId,
    long InstrumentId,
    DateTime Date,
    PortfolioMovementKind Kind,
    decimal Amount,
    string Currency,
    decimal? Quantity = null);