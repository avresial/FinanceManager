using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Per-calculation memo of listing prices in one target currency, so a listing valued on the same
/// day by several steps (opening/ending value, FX intervals) is priced once.
/// </summary>
internal sealed class PortfolioPriceCache(IInvestmentPriceProvider investmentPriceProvider, Currency targetCurrency)
{
    private readonly Dictionary<(long ListingId, DateTime Date), decimal> _prices = [];

    public Currency TargetCurrency { get; } = targetCurrency;

    public async Task<decimal> GetAsync(long listingId, DateTime date, CancellationToken cancellationToken)
    {
        var key = (listingId, date.Date);
        if (_prices.TryGetValue(key, out var price))
            return price;

        price = await investmentPriceProvider.GetPricePerUnitAsync(
            listingId,
            TargetCurrency,
            DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
            cancellationToken);
        _prices[key] = price;
        return price;
    }

    /// <summary>Total value of the holdings on <paramref name="date"/>; incomplete when any held listing has no positive price.</summary>
    public async Task<(decimal Value, bool Complete)> GetHoldingsValueAsync(
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> holdingsByAccount,
        DateTime date,
        CancellationToken cancellationToken)
    {
        decimal total = 0m;
        foreach (var holdings in holdingsByAccount.Values)
        {
            foreach (var (listingId, quantity) in holdings)
            {
                if (quantity == 0m) continue;

                var price = await GetAsync(listingId, date, cancellationToken);
                if (price <= 0m)
                    return (0m, false);

                total += quantity * price;
            }
        }

        return (total, true);
    }
}