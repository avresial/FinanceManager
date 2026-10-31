namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

public sealed record DividendHoldingOption(long AssetListingId, string Name, string Ticker, string? ExchangeMic, string? TradingCurrency);