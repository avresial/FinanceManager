using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// The money-flow series behind the dashboard charts and the money-flow page.
/// </summary>
/// <remarks>
/// These are the endpoints a user hits repeatedly while changing the date range, so their cost is felt
/// as chart lag rather than as page-load time. They all walk the same entry history and differ only in
/// how they fold it, which makes the spread between them a direct read on where the folding is
/// expensive. <c>GetNetWorth</c> is measured both as a single-date point read and as a full series so
/// the per-day cost is separable from the fixed cost.
/// </remarks>
[BenchmarkCategory("MoneyFlow")]
public class MoneyFlowBenchmarks : ApiBenchmark
{
    protected override async Task Prime()
    {
        await NetWorth_SingleDate();
        await NetWorth_Series();
        await Inflow();
        await Outflow();
        await NetCashFlow();
        await ClosingBalance();
        await LabelsValue();
        await InvestmentRate();
    }

    [Benchmark(Description = "GET api/MoneyFlow/GetNetWorth (single date)", Baseline = true)]
    public Task<long> NetWorth_SingleDate() =>
        Get($"api/MoneyFlow/GetNetWorth/{Scenario.UserId}/{Scenario.CurrencyId}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/MoneyFlow/GetNetWorth (series)")]
    public Task<long> NetWorth_Series() => Get(Range("GetNetWorth"));

    [Benchmark(Description = "GET api/MoneyFlow/GetInflow")]
    public Task<long> Inflow() => Get(Range("GetInflow"));

    [Benchmark(Description = "GET api/MoneyFlow/GetOutflow")]
    public Task<long> Outflow() => Get(Range("GetOutflow"));

    [Benchmark(Description = "GET api/MoneyFlow/GetNetCashFlow")]
    public Task<long> NetCashFlow() => Get(Range("GetNetCashFlow"));

    [Benchmark(Description = "GET api/MoneyFlow/GetClosingBalance")]
    public Task<long> ClosingBalance() => Get(Range("GetClosingBalance"));

    [Benchmark(Description = "GET api/MoneyFlow/GetLabelsValue")]
    public Task<long> LabelsValue() =>
        Get($"api/MoneyFlow/GetLabelsValue?userId={Scenario.UserId}&start={IsoQuery(Scenario.Start)}&end={IsoQuery(Scenario.End)}");

    [Benchmark(Description = "GET api/MoneyFlow/GetInvestmentRate")]
    public Task<long> InvestmentRate() =>
        Get($"api/MoneyFlow/GetInvestmentRate?userId={Scenario.UserId}&currencyId={Scenario.CurrencyId}"
            + $"&start={IsoQuery(Scenario.Start)}&end={IsoQuery(Scenario.End)}");

    private string Range(string action) =>
        $"api/MoneyFlow/{action}/{Scenario.UserId}/{Scenario.CurrencyId}/{Iso(Scenario.Start)}/{Iso(Scenario.End)}";
}