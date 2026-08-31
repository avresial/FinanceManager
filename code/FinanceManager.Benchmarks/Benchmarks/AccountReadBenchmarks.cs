using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Account listing and detail reads — what the account pages call before loading entry history.
/// </summary>
[BenchmarkCategory("Accounts")]
public class AccountReadBenchmarks : ApiBenchmark
{
    protected override async Task Prime()
    {
        await CurrencyAccounts_List();
        await CurrencyAccount_Summary();
        await InvestmentAccounts_List();
        await InvestmentAccount_Details();
        await InvestmentTransactions_ByAccount();
        await InvestmentValuation_Holdings();
        await InvestmentValuation_Value();
        await InvestmentValuation_TransactionValuations();
        await BondAccounts_List();
    }

    [Benchmark(Description = "GET api/CurrencyAccount (list)", Baseline = true)]
    public Task<long> CurrencyAccounts_List() => Get("api/CurrencyAccount");

    [Benchmark(Description = "GET api/CurrencyAccount/{id} (summary)")]
    public Task<long> CurrencyAccount_Summary() =>
        Get($"api/CurrencyAccount/{Scenario.PrimaryCurrencyAccountId}");

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

}