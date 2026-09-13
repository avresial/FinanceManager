using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>
/// Calculates the unrealized gain/loss for one investment account without valuing the rest of the
/// user's portfolio.
/// </summary>
public interface IInvestmentAppreciationService
{
    /// <summary>
    /// Returns the selected account's appreciation when it belongs to <paramref name="userId"/>.
    /// An account owned by another user is treated as not found.
    /// </summary>
    Task<UnrealizedGainLossAccountResult?> GetForAccountAsync(
        int userId,
        int accountId,
        Currency currency,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);
}