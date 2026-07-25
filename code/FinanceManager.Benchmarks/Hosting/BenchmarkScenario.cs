namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// The concrete inputs every benchmarked request is built from: the ids that exist in the seeded
/// dataset and the date window that actually contains data.
/// </summary>
/// <remarks>
/// Resolved from the running API during setup rather than hard-coded, because the seeders assign
/// account ids and anchor the history to <c>UtcNow</c> at the moment the template was built. A
/// hard-coded window would drift into empty ranges as the template ages and the endpoints would
/// quietly start measuring "return nothing".
/// </remarks>
/// <param name="UserId">Owner of the seeded dataset; also the subject of the benchmark JWT.</param>
/// <param name="CurrencyId">Reporting currency the aggregate endpoints convert into.</param>
/// <param name="Start">First day covered by the seeded history.</param>
/// <param name="End">Last day covered by the seeded history.</param>
/// <param name="CurrencyAccountIds">Cash and loan accounts, in the order the API lists them.</param>
/// <param name="InvestmentAccountIds">Investment accounts holding the seeded ETF position.</param>
/// <param name="BondAccountIds">Bond accounts holding the seeded bond ladder.</param>
public sealed record BenchmarkScenario(
    int UserId,
    int CurrencyId,
    DateTime Start,
    DateTime End,
    IReadOnlyList<int> CurrencyAccountIds,
    IReadOnlyList<int> InvestmentAccountIds,
    IReadOnlyList<int> BondAccountIds)
{
    /// <summary>Primary cash account — the one the transaction list and entry writes target.</summary>
    public int PrimaryCurrencyAccountId => First(CurrencyAccountIds, nameof(CurrencyAccountIds));

    /// <summary>Primary investment account, used by the holdings and valuation endpoints.</summary>
    public int PrimaryInvestmentAccountId => First(InvestmentAccountIds, nameof(InvestmentAccountIds));

    /// <summary>Primary bond account.</summary>
    public int PrimaryBondAccountId => First(BondAccountIds, nameof(BondAccountIds));

    /// <summary>
    /// A one-month tail of the history, matching the range the account and dashboard views open on by
    /// default. Kept separate from the full window so a benchmark can show how cost scales with range.
    /// </summary>
    public DateTime LastMonthStart => End.AddMonths(-1) < Start ? Start : End.AddMonths(-1);

    private static int First(IReadOnlyList<int> ids, string name) =>
        ids.Count > 0
            ? ids[0]
            : throw new InvalidOperationException(
                $"The seeded dataset contains no {name}. Re-seed with FM_BENCH_RESEED=1; if that does not help, a seeder has stopped producing data.");
}