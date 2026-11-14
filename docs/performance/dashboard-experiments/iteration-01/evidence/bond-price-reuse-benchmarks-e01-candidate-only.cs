using BenchmarkDotNet.Attributes;
using FinanceManager.Benchmarks.Hosting;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// E01: reuse bond price history across dates inside the per-day pricing loop.
/// </summary>
/// <remarks>
/// This is an in-process benchmark rather than an endpoint one — it does not route anything through
/// MVC, so it isolates exactly what E01 targets: how much <see cref="BondAccount.GetDailyPrice"/> spends
/// rebuilding the per-entry price history versus reusing a memoized price across dates. Both measurements
/// run the same production method on the same seeded bond fixture; they differ only in whether the daily
/// lookups reuse cached prices, which mirrors what <c>BondDashboardContext.GetOrComputePrice</c> already
/// does per request. The window and seed data are identical to every other benchmark here, so the A/B is a
/// direct read on cross-date price reuse, not on anything else.
/// </remarks>
[BenchmarkCategory("Bond")]
public class BondPriceReuseBenchmarks
{
    private BenchmarkEnvironment _env = default!;

    /// <summary>The single long-lived bond account loaded into memory during setup.</summary>
    private readonly BondAccount _account = new(0, 0, "Bond", [], AccountLabel.Bond, [], []);

    /// <summary>In-range entries plus the next-older boundary per instrument — the exact inputs GetDailyPrice consumes.</summary>
    private readonly List<BondAccountEntry> _entries = [];

    /// <summary>Next-older boundary entries per instrument, keyed by bond details id.</summary>
    private readonly Dictionary<int, BondAccountEntry> _nextOlder = [];

    /// <summary>The referenced bond definitions the fixture prices against.</summary>
    private readonly List<BondDetails> _details = [];

    /// <summary>Window start the pricing loop is measured over — identical seed window to every benchmark here.</summary>
    private DateOnly _start;

    /// <summary>Memoized price lookup, keyed on entry + date so a reused price never recomputes its history. Mirrors
    /// <c>BondDashboardContext.GetOrComputePrice</c>, which computes each price at most once per request.</summary>
    private readonly Dictionary<(int EntryId, int BondDetailsId, DateOnly Date), decimal> _memoizedPrices = [];

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _env = await BenchmarkEnvironment.GetOrCreate();

        using var scope = _env.CreateHostScope();
        var bondEntryRepository = scope.ServiceProvider.GetRequiredService<IBondAccountEntryRepository<BondAccountEntry>>();
        var bondDetailsRepository = scope.ServiceProvider.GetRequiredService<IBondDetailsRepository>();

        var accountId = _env.Scenario.BondAccountIds[0];

        // In-range entries plus the next-older boundary per instrument — the exact inputs GetDailyPrice consumes.
        _entries = await bondEntryRepository.GetRange([accountId], _env.Scenario.Start, _env.Scenario.End);
        var boundaryPerInstrument = await bondEntryRepository.GetNextOlderPerInstrument([accountId], _env.Scenario.Start);
        foreach (var carried in boundaryPerInstrument.GetValueOrDefault(accountId).Values)
            _nextOlder[carried.BondDetailsId] = carried;

        var referencedIds = _entries.Select(e => e.BondDetailsId)
            .Concat(_nextOlder.Keys)
            .Distinct()
            .ToList();
        _details = await bondDetailsRepository.GetByIdsAsync(referencedIds);

        if (_entries.Count == 0 || _details.Count == 0)
            throw new InvalidOperationException(
                $"Bond account {accountId} has no entries or referenced details, so there is no price history to measure.");

        _account = new BondAccount(_env.Scenario.UserId, accountId, "Bond", [.. _entries], AccountLabel.Bond, [.. _nextOlder]);
        _start = DateOnly.FromDateTime(_env.Scenario.Start);
    }

    /// <summary>Baseline: the raw per-entry pricing path (<paramref name="priceAt"/> null). Every entry rebuilds its
    /// full price series up to the interval end via <see cref="BondAccountEntry.GetPrice(DateOnly, BondDetails)"/>.</summary>
    [Benchmark(Description = "GetDailyPrice over window (no cross-date price reuse)", Baseline = true)]
    public long BondPriceBaseline_NoReuse() =>
        _account.GetDailyPrice(_start, DateOnly.FromDateTime(_env.Scenario.End), _details).Count;

    /// <summary>Non-baseline: the same method with a memoized price callback that reuses each date lookup once per
    /// entry — faithful to E01's already-implemented cross-date reuse.</summary>
    [Benchmark(Description = "GetDailyPrice over window (memoized cross-date price reuse)")]
    public long BondPriceReuse_Memoized() =>
        _account.GetDailyPrice(_start, DateOnly.FromDateTime(_env.Scenario.End), _details, PriceAt);

    /// <summary>Memoized price lookup keyed on entry + date so a reused price never recomputes its history.</summary>
    private decimal PriceAt(BondAccountEntry entry, BondDetails details, DateOnly date)
    {
        var key = (entry.EntryId, entry.BondDetailsId, date);
        if (_memoizedPrices.TryGetValue(key, out var price))
            return price;

        price = entry.GetPriceAt(date, details);
        _memoizedPrices[key] = price;

        return price;
    }
}
