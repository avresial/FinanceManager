using BenchmarkDotNet.Attributes;
using FinanceManager.Contracts.Transactions.Cash;

namespace FinanceManager.Benchmarks.Benchmarks;

[BenchmarkCategory("Writes")]
public class CurrencyEntryWriteBenchmarks : ApiBenchmark
{
    private DateTime _appendDate;

    protected override void Configure() => _appendDate = DateTime.SpecifyKind(Scenario.End.AddDays(1), DateTimeKind.Utc);

    protected override async Task Prime()
    {
        await AddEntry();
        Cleanup();
    }

    [IterationCleanup]
    public void Cleanup() => Api.ResetAppendedEntries(_appendDate);

    [Benchmark(Description = "POST api/transactions/cash", Baseline = true)]
    public Task<long> AddEntry() =>
        Post("api/transactions/cash", new CreateCashEntryRequest(Scenario.CurrencyId, _appendDate, -25m, "Benchmark append", null));
}