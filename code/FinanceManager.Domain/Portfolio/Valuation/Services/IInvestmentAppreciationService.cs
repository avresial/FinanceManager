using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.Portfolio.Valuation.Services;

/// <summary>Calculates unrealized gain or loss across the owner's investment holdings.</summary>
public interface IInvestmentAppreciationService
{
    Task<UnrealizedGainLossResult?> GetForUserAsync(int userId, Currency currency, DateTime asOfDate, CancellationToken ct = default);
}