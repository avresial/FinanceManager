namespace FinanceManager.Benchmarks.Hosting;

/// <summary>The owner and currency of the seeded cash history, with its measured date window.</summary>
public sealed record BenchmarkScenario(int UserId, int CurrencyId, DateTime Start, DateTime End)
{
    public DateTime LastMonthStart => End.AddMonths(-1) < Start ? Start : End.AddMonths(-1);
}