using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Entry-history reads used by the currency and bond account detail pages.
/// </summary>
/// <remarks>
/// Each account type is measured over the full selected range and in both paging directions. The
/// full-range calls expose work that scales with history size, while the fixed-size pages isolate the
/// account lookup, boundary-entry queries, mapping, and serialization paid on every scroll.
/// </remarks>
[BenchmarkCategory("FinancialAccountEntries")]
public class FinancialAccountEntryReadBenchmarks : ApiBenchmark
{
    private const int _pageSize = 50;

    protected override async Task Prime()
    {
        await Currency_FullRange();
        await Currency_OlderPage();
        await Currency_NewerPage();
        await Bond_FullRange();
        await Bond_OlderPage();
        await Bond_NewerPage();
    }

    /// <summary>
    /// The currency route separates its segments with '&amp;' rather than '/', matching the route used by
    /// <c>CurrencyAccountHttpClient</c>.
    /// </summary>
    [Benchmark(Description = "GET api/CurrencyAccount/{id}&{start}&{end} (full range)", Baseline = true)]
    public Task<long> Currency_FullRange() =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}&{Iso(Scenario.Start)}&{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/CurrencyAccount/{id}/entries (50 older)")]
    public Task<long> Currency_OlderPage() => GetCurrencyPage(Scenario.End, olderThanDate: true);

    [Benchmark(Description = "GET api/CurrencyAccount/{id}/entries (50 newer)")]
    public Task<long> Currency_NewerPage() => GetCurrencyPage(Scenario.Start, olderThanDate: false);

    [Benchmark(Description = "GET api/BondAccount/{id}/{start}/{end} (full range)")]
    public Task<long> Bond_FullRange() =>
        Get($"api/BondAccount/{Scenario.PrimaryBondAccountId}/{Iso(Scenario.Start)}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/BondAccount/{id}/entries (50 older)")]
    public Task<long> Bond_OlderPage() => GetBondPage(Scenario.End, olderThanDate: true);

    [Benchmark(Description = "GET api/BondAccount/{id}/entries (50 newer)")]
    public Task<long> Bond_NewerPage() => GetBondPage(Scenario.Start, olderThanDate: false);

    private Task<long> GetCurrencyPage(DateTime date, bool olderThanDate) =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}/entries"
            + $"?date={IsoQuery(date)}&count={_pageSize}&olderThenDate={olderThanDate.ToString().ToLowerInvariant()}");

    private Task<long> GetBondPage(DateTime date, bool olderThanDate) =>
        Get($"api/BondAccount/{Scenario.PrimaryBondAccountId}/entries"
            + $"?date={IsoQuery(date)}&count={_pageSize}&olderThenDate={olderThanDate.ToString().ToLowerInvariant()}");
}