namespace FinanceManager.Application.Portfolio.Performance;

internal sealed record PortfolioValuationSeries(
    IReadOnlyDictionary<DateTime, decimal> Values,
    decimal OpeningValue,
    bool Complete)
{
    public static PortfolioValuationSeries Incomplete { get; } = new(new Dictionary<DateTime, decimal>(), 0m, false);
}