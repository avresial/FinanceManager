using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>
/// Values the owner's pooled investment holdings. Holdings are computed on read from every
/// <see cref="Entities.InvestmentTransaction"/> of the user (linked to an account or not; signed Buy/Sell quantities per
/// <see cref="AssetListing"/>) rather than from a stored running balance, and valued through
/// <see cref="Services.IInvestmentPriceProvider"/> (which already normalises GBX-style quotes and
/// converts to the requested currency).
/// </summary>
public interface IInvestmentValuationService
{
    /// <summary>
    /// Value per <see cref="AssetListing"/> of the owner's pooled holdings (every trade of <paramref name="userId"/>, linked to an account or not)
    /// converted to <paramref name="targetCurrency"/> as of <paramref name="asOf"/>. Listings without a net holding or without a price are omitted;
    /// an oversold listing keeps a negative value.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetUserValueByListingAsync(int userId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Daily value of the owner's pooled holdings in <paramref name="targetCurrency"/> over [<paramref name="start"/>, <paramref name="end"/>], each listing priced from its own per-day series
    /// (holdings are carried forward on days without a trade) with an explicit zero on the last day.
    /// Empty when the owner has no holding or trade in the window.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetUserValueSeriesAsync(int userId, Currency targetCurrency, DateTime start, DateTime end, CancellationToken ct = default);
}