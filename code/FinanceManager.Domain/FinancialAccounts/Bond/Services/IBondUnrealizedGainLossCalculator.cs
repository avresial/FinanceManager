using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Services;

public interface IBondUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(BondAccount account, int bondDetailsId, BondDetails? details, Currency currency, DateTime asOfDate);
}