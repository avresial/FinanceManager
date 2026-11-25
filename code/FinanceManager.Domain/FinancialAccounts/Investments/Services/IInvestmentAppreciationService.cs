using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>Calculates unrealized gain or loss across the owner's investment holdings.</summary>
public interface IInvestmentAppreciationService
{
    Task<UnrealizedGainLossAccountResult?> GetForUserAsync(int userId, Currency currency, DateTime asOfDate, CancellationToken ct = default);
}