using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;

namespace FinanceManager.Application.Transactions.Bonds.Valuation;

/// <summary>
/// Request-scoped context shared by the bond dashboard paths (net worth, closing balance, ...).
/// It loads only the bond definitions actually referenced by the given accounts through a single
/// no-tracking query, stores detached snapshots, and memoizes the deterministic per-account,
/// per-bond, per-date prices so every price is computed at most once per request.
/// </summary>
public class BondDashboardContext(IBondDetailsRepository bondDetailsRepository)
{
    private readonly Dictionary<int, BondDetails> _details = [];
    private readonly HashSet<int> _requestedIds = [];

    private readonly Dictionary<(int BondDetailsId, DateOnly AnchorDate, decimal Units, DateOnly Date), decimal> _pooledPrices = [];

    /// <summary>Loads the definitions of the given bond ids, with the same once-per-request behaviour as the account overload.</summary>
    public async Task<IReadOnlyDictionary<int, BondDetails>> LoadReferencedDetailsAsync(
        IEnumerable<int> bondDetailsIds,
        CancellationToken cancellationToken = default)
    {
        var missing = bondDetailsIds
            .Where(id => !_requestedIds.Contains(id))
            .Distinct()
            .ToList();

        if (missing.Count > 0)
        {
            var loaded = await bondDetailsRepository.GetByIdsAsync(missing, cancellationToken);
            _requestedIds.UnionWith(missing);
            foreach (var details in loaded)
                _details[details.Id] = details;
        }

        return _details;
    }

    /// <summary>
    /// Returns the stored definition, throwing for a deleted bond exactly like the previous dashboard
    /// paths did when their details dictionary had no entry for the id.
    /// </summary>
    public BondDetails GetDetails(int bondDetailsId)
    {
        if (!_details.TryGetValue(bondDetailsId, out var details))
            throw new InvalidOperationException($"Bond valuation requires details for bond id {bondDetailsId}.");

        return details;
    }

    /// <summary>
    /// Returns the memoized value of <paramref name="units"/> of a bond accruing from <paramref name="anchorDate"/> on the date, for owner-pooled
    /// positions. Like the entry overload this is a pure function, so services that value the same pooled position within one request share a computation.
    /// </summary>
    public decimal GetOrComputePrice(int bondDetailsId, DateOnly anchorDate, decimal units, DateOnly date, Func<decimal>? calculatePrice = null)
    {
        var key = (bondDetailsId, anchorDate, units, date);
        if (_pooledPrices.TryGetValue(key, out var price))
            return price;

        price = calculatePrice is null
            ? date < anchorDate ? 0 : BondEntry.GetPrice(anchorDate, units, date, GetDetails(bondDetailsId)).GetValueOrDefault(date)
            : calculatePrice();
        _pooledPrices[key] = price;

        return price;
    }
}