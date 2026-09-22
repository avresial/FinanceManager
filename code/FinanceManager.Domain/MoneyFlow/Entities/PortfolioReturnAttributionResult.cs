namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// Portfolio value change decomposed into dated cash movement, known fees, currency translation,
/// and a residual investment effect for the selected range.
/// </summary>
public sealed record PortfolioReturnAttributionResult(
    PortfolioReturnAttributionStatus Status,
    decimal? TotalChange,
    decimal? ExternalCashMovement,
    decimal? MarketEffect,
    decimal? DividendIncome,
    decimal? FeeEffect,
    decimal? FxEffect,
    decimal? ReconciliationDifference,
    DateTime StartDate,
    DateTime EndDate,
    List<UnsupportedAttributionComponent> UnsupportedComponents)
{
    public bool IsAvailable => Status == PortfolioReturnAttributionStatus.Available
        && TotalChange.HasValue
        && ExternalCashMovement.HasValue
        && MarketEffect.HasValue
        && FeeEffect.HasValue
        && FxEffect.HasValue
        && ReconciliationDifference.HasValue;

    public static PortfolioReturnAttributionResult InsufficientData(DateTime startDate, DateTime endDate) =>
        Create(PortfolioReturnAttributionStatus.InsufficientData, null, null, null, null, null, null, null, startDate, endDate);

    public static PortfolioReturnAttributionResult Unavailable(DateTime startDate, DateTime endDate) =>
        Create(PortfolioReturnAttributionStatus.Unavailable, null, null, null, null, null, null, null, startDate, endDate);

    public static PortfolioReturnAttributionResult Available(
        decimal totalChange,
        decimal externalCashMovement,
        decimal marketEffect,
        decimal feeEffect,
        decimal fxEffect,
        DateTime startDate,
        DateTime endDate) =>
        Create(
            PortfolioReturnAttributionStatus.Available,
            totalChange,
            externalCashMovement,
            marketEffect,
            null,
            feeEffect,
            fxEffect,
            totalChange - (externalCashMovement + marketEffect + feeEffect + fxEffect),
            startDate,
            endDate);

    private static PortfolioReturnAttributionResult Create(
        PortfolioReturnAttributionStatus status,
        decimal? totalChange,
        decimal? externalCashMovement,
        decimal? marketEffect,
        decimal? dividendIncome,
        decimal? feeEffect,
        decimal? fxEffect,
        decimal? reconciliationDifference,
        DateTime startDate,
        DateTime endDate) =>
        new(
            status,
            totalChange,
            externalCashMovement,
            marketEffect,
            dividendIncome,
            feeEffect,
            fxEffect,
            reconciliationDifference,
            startDate,
            endDate,
            [
                new("Dividends / income", "Dividend transactions are not persisted yet."),
                new("ETF expense-ratio fees", "Expense ratios are available for projections only; realized charges are not recorded."),
            ]);
}