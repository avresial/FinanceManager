using FinanceManager.Contracts.Portfolio;
using FinanceManager.Domain.Currencies.Entities;

namespace FinanceManager.Application.Portfolio.Valuation;

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
    /// Valuation figures for the Buy transactions of <paramref name="userId"/> among <paramref name="transactionIds"/>, linked to an
    /// account or not. Sell transactions and ids of other users are omitted. The caller bounds the id list (the API caps it per request).
    /// </summary>
    Task<IReadOnlyList<InvestmentTransactionValuationDto>> GetForUserAsync(
        int userId,
        Currency targetCurrency,
        IReadOnlyCollection<long> transactionIds,
        CancellationToken cancellationToken = default);
}