using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// A single daily price point returned by a market-data price source (Alpha Vantage, EODHD, …).
/// Used by the investment pricing chain to populate <see cref="Assets.Entities.PriceQuote"/> rows;
/// it is a transient value object, not a persisted entity.
/// </summary>
public class StockPrice
{
    public required string Isin { get; set; }
    public decimal PricePerUnit { get; set; }
    public required Currency Currency { get; set; }
    public DateTime Date { get; set; }
}