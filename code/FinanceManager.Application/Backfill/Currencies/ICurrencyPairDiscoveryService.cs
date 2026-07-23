namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>A normalized, upper-cased currency pair to backfill (<see cref="From"/> → <see cref="To"/>).</summary>
public readonly record struct CurrencyPair(string From, string To);

/// <summary>
/// Discovers the distinct currency pairs the system needs exchange rates for: every currency an
/// instrument trades in, valued into every currency a user has chosen to see their portfolio in.
/// </summary>
public interface ICurrencyPairDiscoveryService
{
    /// <summary>
    /// Distinct source → target pairs, normalized (trimmed, upper-cased, pence-style minor units
    /// collapsed to their major currency) and de-duplicated, excluding same-currency pairs.
    /// </summary>
    Task<IReadOnlyList<CurrencyPair>> GetPairsAsync(CancellationToken cancellationToken = default);
}