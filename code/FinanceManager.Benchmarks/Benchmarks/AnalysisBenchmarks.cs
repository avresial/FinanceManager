using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// The analysis cards: expense distribution, essential spending, diversification and liabilities.
/// </summary>
/// <remarks>
/// Each of these backs a card the dashboard renders on load, so they run concurrently with the
/// money-flow and asset queries in real use. Benchmarked one at a time here — this suite measures the
/// per-endpoint cost, not contention between them.
/// </remarks>
[BenchmarkCategory("Analysis")]
public class AnalysisBenchmarks : ApiBenchmark
{
    protected override async Task Prime()
    {
        await ExpenseDistribution();
        await EssentialSpending();
        await DiversificationScore();
        await DiversificationBreakdown();
        await IsAnyAccountWithLiabilities();
        await EndLiabilitiesPerAccount();
        await EndLiabilitiesPerType();
        await LiabilitiesTimeSeries();
    }

    [Benchmark(Description = "GET api/ExpenseDistribution/GetExpenseDistribution", Baseline = true)]
    public Task<long> ExpenseDistribution() =>
        Get($"api/ExpenseDistribution/GetExpenseDistribution/{Scenario.UserId}/{Scenario.CurrencyId}"
            + $"/{Iso(Scenario.Start)}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/EssentialSpending/GetEssentialSpending")]
    public Task<long> EssentialSpending() =>
        Get($"api/EssentialSpending/GetEssentialSpending/{Scenario.UserId}/{Scenario.CurrencyId}"
            + $"/{Iso(Scenario.Start)}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/Diversification (score)")]
    public Task<long> DiversificationScore() =>
        Get($"api/Diversification/{Scenario.UserId}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/Diversification (breakdown)")]
    public Task<long> DiversificationBreakdown() =>
        Get($"api/Diversification/{Scenario.UserId}/{Iso(Scenario.End)}/breakdown");

    [Benchmark(Description = "GET api/Liabilities/IsAnyAccountWithLiabilities")]
    public Task<long> IsAnyAccountWithLiabilities() =>
        Get($"api/Liabilities/IsAnyAccountWithLiabilities/{Scenario.UserId}");

    [Benchmark(Description = "GET api/Liabilities/GetEndLiabilitiesPerAccount")]
    public Task<long> EndLiabilitiesPerAccount() => Get(Liabilities("GetEndLiabilitiesPerAccount"));

    [Benchmark(Description = "GET api/Liabilities/GetEndLiabilitiesPerType")]
    public Task<long> EndLiabilitiesPerType() => Get(Liabilities("GetEndLiabilitiesPerType"));

    [Benchmark(Description = "GET api/Liabilities/GetLiabilitiesTimeSeries")]
    public Task<long> LiabilitiesTimeSeries() => Get(Liabilities("GetLiabilitiesTimeSeries"));

    private string Liabilities(string action) =>
        $"api/Liabilities/{action}/{Scenario.UserId}/{Iso(Scenario.Start)}/{Iso(Scenario.End)}";
}