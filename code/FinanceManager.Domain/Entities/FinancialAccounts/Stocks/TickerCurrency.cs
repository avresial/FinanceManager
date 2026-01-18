using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Entities.Stocks;

public record TickerCurrency(string Ticker, Currency Currency);