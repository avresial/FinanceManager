using BenchmarkDotNet.Attributes;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.Shared;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Microbenchmark of the domain price-history mechanism for long-lived bonds (E01): repeated point
/// pricing via <see cref="BondAccountEntry.GetPriceAt"/> versus one prepared history per request.
/// </summary>
/// <remarks>
/// This measures the DOMAIN computation only — how expensive it is to produce and read bond price
/// values in <c>BondAccountEntry</c>. It deliberately does NOT measure request-context dictionary
/// overhead or API/page performance: there is no HTTP host, service layer, or per-request context here.
/// <see cref="PreparedHistory"/> includes the FULL preparation cost because it rebuilds
/// <c>GetPrice(end)</c> inside every invocation; nothing is cached globally between invocations.
/// </remarks>
public class BondHistoryBenchmarks
{
    // Exact fixture (no clock dependency): posting 2010-01-01 UTC, window end = posting + 3650 days.
    private static readonly DateTime PostingDate = new(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly EndDate = DateOnly.FromDateTime(PostingDate.AddDays(3650));

    [Params(31, 183)]
    public int WindowDays { get; set; }

    [Params(false, true)]
    public bool Descending { get; set; }

    private BondAccountEntry _entry = null!;
    private BondDetails _details = null!;
    private DateOnly[] _dates = null!;

    /// <summary>
    /// Creates the immutable fixture once per parameter combination, then validates the mechanism:
    /// every date's point-pricing value must equal its prepared-history lookup (throws on mismatch),
    /// and both benchmark methods must return the same checksum. No timing assertions are made.
    /// </summary>
    [GlobalSetup]
    public void CreateFixture()
    {
        _entry = new BondAccountEntry(1, 1, PostingDate, 1000m, 1000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2030-01-01",
            Rate = 0.0365m
        };

        _details = new BondDetails(
            "E01 Long-Lived Bond",
            "Benchmark Issuer",
            DateOnly.FromDateTime(PostingDate),
            EndDate,
            [calculationMethod],
            unitValue: 100m
        );

        // Exactly WindowDays consecutive dates ending at EndDate; reversed when Descending.
        var dates = new List<DateOnly>(WindowDays);
        for (var offset = WindowDays - 1; offset >= 0; offset--)
            dates.Add(EndDate.AddDays(-offset));
        if (Descending)
            dates.Reverse();
        _dates = [.. dates];

        // Verify EACH date's per-point value against a full history lookup.
        var history = _entry.GetPrice(EndDate, _details);
        foreach (var date in _dates)
        {
            var pointValue = _entry.GetPriceAt(date, _details);
            if (!history.TryGetValue(date, out var historyValue))
                throw new InvalidOperationException($"E01 mechanism mismatch at {date:yyyy-MM-dd}: date missing from prepared history.");
            if (pointValue != historyValue)
                throw new InvalidOperationException($"E01 mechanism mismatch at {date:yyyy-MM-dd}: GetPriceAt={pointValue}, history lookup={historyValue}.");
        }

        // Both benchmark methods must agree on the window checksum.
        var repeated = RepeatedPointPricing();
        var prepared = PreparedHistory();
        if (repeated != prepared)
            throw new InvalidOperationException($"E01 mechanism checksum mismatch: repeated={repeated}, prepared={prepared}.");
    }

    /// <summary>Baseline: one <see cref="BondAccountEntry.GetPriceAt"/> per date; each call recomputes the history up to that date.</summary>
    [Benchmark(Baseline = true, Description = "Repeated point pricing (GetPriceAt per date)")]
    public decimal RepeatedPointPricing()
    {
        var checksum = 0m;
        foreach (var date in _dates)
            checksum += _entry.GetPriceAt(date, _details);
        return checksum;
    }

    /// <summary>One full history built INSIDE every invocation (<c>GetPrice(end)</c>), then dictionary lookups per date.</summary>
    [Benchmark(Description = "Prepared history (GetPrice(end) rebuilt each invocation + lookup)")]
    public decimal PreparedHistory()
    {
        var history = _entry.GetPrice(EndDate, _details);

        var checksum = 0m;
        foreach (var date in _dates)
            checksum += history.GetValueOrDefault(date);
        return checksum;
    }
}
