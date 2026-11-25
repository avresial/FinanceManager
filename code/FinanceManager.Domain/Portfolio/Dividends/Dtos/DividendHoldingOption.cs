namespace FinanceManager.Domain.Portfolio.Dividends.Dtos;

public sealed record DividendHoldingOption(long AssetListingId, string Name, string Ticker, string? ExchangeMic, string? TradingCurrency);