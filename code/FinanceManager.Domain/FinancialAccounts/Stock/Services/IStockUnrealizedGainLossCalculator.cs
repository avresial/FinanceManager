using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Services;

public interface IStockUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(StockAccount account, string ticker, Currency currency, DateTime asOfDate);
}