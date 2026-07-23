using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>
/// Builds the backfill's currency-pair set from two concrete sources the domain already exposes:
/// the trading currencies of every held listing (reusing the same distinct-listing query the stock
/// backfill uses) and every distinct user preferred currency. Portfolio valuation converts the
/// former into the latter, so their cross-product is the set of rates the app must be able to serve.
/// </summary>
public sealed class CurrencyPairDiscoveryService(
    IInvestmentTransactionRepository transactionRepository,
    IAssetListingRepository listingRepository,
    IUserRepository userRepository,
    ICurrencyRepository currencyRepository,
    ILogger<CurrencyPairDiscoveryService> logger) : ICurrencyPairDiscoveryService
{
    public async Task<IReadOnlyList<CurrencyPair>> GetPairsAsync(CancellationToken cancellationToken = default)
    {
        var sources = await GetSourceCurrenciesAsync(cancellationToken);
        var targets = await GetTargetCurrenciesAsync(cancellationToken);

        var pairs = new HashSet<CurrencyPair>();
        foreach (var from in sources)
        {
            foreach (var to in targets)
            {
                if (!string.Equals(from, to, StringComparison.Ordinal))
                    pairs.Add(new CurrencyPair(from, to));
            }
        }

        logger.LogInformation(
            "Currency backfill discovered {PairCount} pairs from {SourceCount} traded currencies and {TargetCount} preferred currencies.",
            pairs.Count, sources.Count, targets.Count);

        return pairs.ToList();
    }

    private async Task<IReadOnlyCollection<string>> GetSourceCurrenciesAsync(CancellationToken ct)
    {
        var listingIds = await transactionRepository.GetDistinctAssetListingIds(ct);
        var currencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var listingId in listingIds)
        {
            ct.ThrowIfCancellationRequested();

            var listing = await listingRepository.Get(listingId, ct);
            if (listing is null || string.IsNullOrWhiteSpace(listing.TradingCurrency)) continue;

            currencies.Add(Normalize(listing.TradingCurrency));
        }

        return currencies;
    }

    private async Task<IReadOnlyCollection<string>> GetTargetCurrenciesAsync(CancellationToken ct)
    {
        var currencyIds = await userRepository.GetDistinctPreferredCurrencyIds(ct);
        var currencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var currencyId in currencyIds)
        {
            ct.ThrowIfCancellationRequested();

            var currency = await currencyRepository.GetCurrency(currencyId, ct);
            if (currency is null || string.IsNullOrWhiteSpace(currency.ShortName)) continue;

            currencies.Add(Normalize(currency.ShortName));
        }

        return currencies;
    }

    // Match the persistence layer's normalisation (trim + upper) and collapse pence-style minor
    // units (GBX → GBP) so a listing quoted in GBX resolves rates the FX provider actually knows.
    private static string Normalize(string currency)
    {
        var trimmed = currency.Trim().ToUpperInvariant();
        return DefaultCurrency.MinorQuoteUnits.TryGetValue(trimmed, out var major) ? major : trimmed;
    }
}