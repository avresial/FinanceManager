using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Bond.Valuation;

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
    private readonly Dictionary<(int AccountId, int EntryId, int BondDetailsId, DateOnly PostingDate, decimal Value, DateOnly Date), decimal> _prices = [];

    /// <summary>
    /// Loads the bond definitions referenced by the accounts (in-range entries plus next-older boundary
    /// entries). Repeated calls within one request only query ids not requested yet, so the union of
    /// references across the dashboard's services costs a single repository round-trip. Missing
    /// definitions are omitted; <see cref="GetDetails"/> keeps the existing throw for them.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, BondDetails>> LoadReferencedDetailsAsync(
        IEnumerable<BondAccount> accounts,
        CancellationToken cancellationToken = default)
    {
        var missing = accounts
            .SelectMany(x => x.GetStoredBondsIds())
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
    /// Returns the memoized price of the entry at the date, computing it only on the first request for
    /// that account/entry/date. Bond pricing is a pure function of the entry and the definition, so two
    /// dashboard paths that materialize the same account within one request share a single computation
    /// without allowing a changed entry snapshot to reuse a stale value.
    /// </summary>
    public decimal GetOrComputePrice(int accountId, BondAccountEntry entry, DateOnly date)
    {
        var key = (accountId, entry.EntryId, entry.BondDetailsId, DateOnly.FromDateTime(entry.PostingDate), entry.Value, date);
        if (_prices.TryGetValue(key, out var price))
            return price;

        price = entry.GetPriceAt(date, GetDetails(entry.BondDetailsId));
        _prices[key] = price;

        return price;
    }
}