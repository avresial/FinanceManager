using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IBondUnrealizedGainLossCalculator
{
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(BondAccount account, int bondDetailsId, BondDetails? details, Currency currency, DateTime asOfDate);
}
