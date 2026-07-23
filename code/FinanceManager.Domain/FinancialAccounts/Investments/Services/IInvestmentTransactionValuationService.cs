using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>
/// Computes per-transaction valuation and performance figures (purchase-day value, current
/// valuation and gain/loss) for the transaction detail view. Buy transactions are priced through
/// <see cref="IInvestmentPriceProvider"/> and converted to a target currency using the historical
/// exchange rate on the trade date for the purchase value and the latest rate for the current
/// valuation. When conversion is unavailable the figures fall back to the transaction currency.
/// </summary>
public interface IInvestmentTransactionValuationService
{
    /// <summary>
    /// Valuation figures keyed by transaction id for every Buy transaction of the account.
    /// Sell transactions are omitted. Returns an empty list for an account with no Buy transactions.
    /// </summary>
    Task<IReadOnlyList<InvestmentTransactionValuationDto>> GetForAccountAsync(
        int accountId,
        Currency targetCurrency,
        CancellationToken cancellationToken = default);
}