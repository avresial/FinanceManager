using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Account listing and detail reads — what the account pages call when a user drills into one account.
/// </summary>
/// <remarks>
/// The interesting contrast here is the full-range read against the paged read. The account detail view
/// can either pull a whole date range or ask for a fixed number of entries around a date; if the paged
/// endpoint is not dramatically cheaper than the range endpoint, paging is not actually saving the user
/// anything and that is a concrete refactor target.
/// </remarks>
[BenchmarkCategory("Accounts")]
public class AccountReadBenchmarks : ApiBenchmark
{
    private const int _pageSize = 50;

    protected override async Task Prime()
    {
        await CurrencyAccounts_List();
        await CurrencyAccount_Summary();
        await CurrencyAccount_DateRange();
        await CurrencyAccount_EntryPage();
        await InvestmentAccounts_List();
        await InvestmentAccount_Details();
        await InvestmentTransactions_ByAccount();
        await InvestmentValuation_Holdings();
        await InvestmentValuation_Value();
        await InvestmentValuation_TransactionValuations();
        await BondAccounts_List();
        await BondAccount_DateRange();
    }

    [Benchmark(Description = "GET api/CurrencyAccount (list)", Baseline = true)]
    public Task<long> CurrencyAccounts_List() => Get("api/CurrencyAccount");

    [Benchmark(Description = "GET api/CurrencyAccount/{id} (summary)")]
    public Task<long> CurrencyAccount_Summary() =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}");

    /// <summary>
    /// The route separates its segments with '&amp;' rather than '/' — that is the actual template
    /// (<c>{accountId}&amp;{startDate}&amp;{endDate}</c>), matching what CurrencyAccountHttpClient builds.
    /// </summary>
    [Benchmark(Description = "GET api/CurrencyAccount/{id}&{start}&{end} (full range)")]
    public Task<long> CurrencyAccount_DateRange() =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}&{Iso(Scenario.Start)}&{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/CurrencyAccount/{id}/entries (page of 50)")]
    public Task<long> CurrencyAccount_EntryPage() =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}/entries"
            + $"?date={IsoQuery(Scenario.End)}&count={_pageSize}&olderThenDate=true");

    [Benchmark(Description = "GET api/InvestmentAccount (list)")]
    public Task<long> InvestmentAccounts_List() => Get("api/InvestmentAccount");

    [Benchmark(Description = "GET api/InvestmentAccount/{id}")]
    public Task<long> InvestmentAccount_Details() =>
        Get($"api/InvestmentAccount/{Scenario.PrimaryInvestmentAccountId}");

    [Benchmark(Description = "GET api/InvestmentTransaction/GetByAccount/{id}")]
    public Task<long> InvestmentTransactions_ByAccount() =>
        Get($"api/InvestmentTransaction/GetByAccount/{Scenario.PrimaryInvestmentAccountId}");

    [Benchmark(Description = "GET api/InvestmentValuation/Holdings/{id}/{date}")]
    public Task<long> InvestmentValuation_Holdings() =>
        Get($"api/InvestmentValuation/Holdings/{Scenario.PrimaryInvestmentAccountId}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/InvestmentValuation/Value/{id}/{currency}/{date}")]
    public Task<long> InvestmentValuation_Value() =>
        Get($"api/InvestmentValuation/Value/{Scenario.PrimaryInvestmentAccountId}/{Scenario.CurrencyId}/{Iso(Scenario.End)}");

    [Benchmark(Description = "GET api/InvestmentValuation/TransactionValuations/{id}/{currency}")]
    public Task<long> InvestmentValuation_TransactionValuations() =>
        Get($"api/InvestmentValuation/TransactionValuations/{Scenario.PrimaryInvestmentAccountId}/{Scenario.CurrencyId}");

    [Benchmark(Description = "GET api/BondAccount (list)")]
    public Task<long> BondAccounts_List() => Get("api/BondAccount");

    [Benchmark(Description = "GET api/BondAccount/{id}/{start}/{end} (full range)")]
    public Task<long> BondAccount_DateRange() =>
        Get($"api/BondAccount/{Scenario.PrimaryBondAccountId}/{Iso(Scenario.Start)}/{Iso(Scenario.End)}");
}