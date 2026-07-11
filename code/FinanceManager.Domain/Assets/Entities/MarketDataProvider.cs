namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Identifies the external market data provider a <see cref="MarketDataSymbol"/> or
/// <see cref="PriceQuote"/> originates from.
/// </summary>
public enum MarketDataProvider
{
    AlphaVantage,
    TwelveData,
    FinancialModelingPrep,
    Eodhd,
    Tiingo,
    Finnhub,
    Stooq,
    YahooFinance,
    Manual
}