using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Services;

public interface IBondUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(BondAccount account, int bondDetailsId, BondDetails? details, Currency currency, DateTime asOfDate);
}