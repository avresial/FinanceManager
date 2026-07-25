using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// The dashboard first-paint aggregate — the single most important number in this suite.
/// </summary>
/// <remarks>
/// <c>api/Dashboard/overview</c> is what the landing page waits on after login, and it fans out
/// internally to net worth, cash flow, expense distribution and asset breakdowns. Whatever this costs
/// is the floor on time-to-first-useful-screen, so it is measured over two windows: the six-month
/// default and a one-month view. If the two are close, the cost is fixed overhead; if the six-month
/// case is much worse, the aggregation scales with the range and that is where to look first.
/// </remarks>
[BenchmarkCategory("Dashboard")]
public class DashboardBenchmarks : ApiBenchmark
{
    protected override async Task Prime()
    {
        await Overview_SixMonths();
        await Overview_OneMonth();
    }

    [Benchmark(Description = "GET api/Dashboard/overview (6 months)", Baseline = true)]
    public Task<long> Overview_SixMonths() => Get(OverviewRoute(Scenario.Start, Scenario.End));

    [Benchmark(Description = "GET api/Dashboard/overview (1 month)")]
    public Task<long> Overview_OneMonth() => Get(OverviewRoute(Scenario.LastMonthStart, Scenario.End));

    private string OverviewRoute(DateTime start, DateTime end) =>
        $"api/Dashboard/overview?userId={Scenario.UserId}&currencyId={Scenario.CurrencyId}&start={IsoQuery(start)}&end={IsoQuery(end)}";
}