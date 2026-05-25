using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Services;

public interface IStockUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(StockAccount account, string ticker, Currency currency, DateTime asOfDate);
}