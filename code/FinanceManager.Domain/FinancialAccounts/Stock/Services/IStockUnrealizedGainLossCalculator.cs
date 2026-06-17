using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Services;

public interface IStockUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(StockAccount account, string ticker, Currency currency, DateTime asOfDate);
}