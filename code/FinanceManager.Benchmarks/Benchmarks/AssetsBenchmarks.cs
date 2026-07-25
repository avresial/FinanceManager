using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Asset valuation and breakdown endpoints.
/// </summary>
/// <remarks>
/// Every one of these prices the seeded holdings against the daily quote series, so they exercise the
/// heaviest read path in the app: transactions joined to listings joined to quotes, then converted into
/// the reporting currency. Expect these to be the slowest reads in the suite and the ones most worth
/// optimizing.
/// </remarks>
[BenchmarkCategory("Assets")]
public class AssetsBenchmarks : ApiBenchmark
{
    protected override async Task Prime()
    {
        await IsAnyAccountWithAssets();
        await EndAssetsPerAccount();
        await EndAssetsPerType();
        await AssetsTimeSeries();
        await InvestmentPaycheckEstimate();
        await UnrealizedGainLossPerAccount();
        await UnrealizedGainLossPerInstrument();
    }

    /// <summary>
    /// Reads as a trivial existence check and gates whether the asset cards render at all, so it sits on
    /// the critical path of every dashboard load. Kept as this category's baseline precisely because it
    /// is <em>not</em> cheap in practice — a boolean that costs the same order as a full breakdown is the
    /// clearest signal in this suite that the query behind it does more work than the answer needs.
    /// </summary>
    [Benchmark(Description = "GET api/Assets/IsAnyAccountWithAssets", Baseline = true)]
    public Task<long> IsAnyAccountWithAssets() =>
        Get($"api/Assets/IsAnyAccountWithAssets/{Scenario.UserId}");

    [Benchmark(Description = "GET api/Assets/GetEndAssetsPerAccount")]
    public Task<long> EndAssetsPerAccount() => Get(AsOf("GetEndAssetsPerAccount"));

    [Benchmark(Description = "GET api/Assets/GetEndAssetsPerType")]
    public Task<long> EndAssetsPerType() => Get(AsOf("GetEndAssetsPerType"));

    [Benchmark(Description = "GET api/Assets/GetAssetsTimeSeries")]
    public Task<long> AssetsTimeSeries() =>
        Get($"api/Assets/GetAssetsTimeSeries/{Scenario.UserId}/{Scenario.CurrencyId}/{Iso(Scenario.Start)}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/Assets/GetInvestmentPaycheckEstimate")]
    public Task<long> InvestmentPaycheckEstimate() =>
        Get($"{AsOf("GetInvestmentPaycheckEstimate")}?withdrawalRate=0.05&salaryMonths=3");

    [Benchmark(Description = "GET api/Assets/GetUnrealizedGainLossPerAccount")]
    public Task<long> UnrealizedGainLossPerAccount() => Get(AsOf("GetUnrealizedGainLossPerAccount"));

    [Benchmark(Description = "GET api/Assets/GetUnrealizedGainLossPerInstrument")]
    public Task<long> UnrealizedGainLossPerInstrument() => Get(AsOf("GetUnrealizedGainLossPerInstrument"));

    private string AsOf(string action) =>
        $"api/Assets/{action}/{Scenario.UserId}/{Scenario.CurrencyId}/{Iso(Scenario.End)}";
}