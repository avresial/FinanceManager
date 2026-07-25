using BenchmarkDotNet.Attributes;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Small reads that fire on nearly every page: the user record, the currency list, labels and the recent
/// transaction log.
/// </summary>
/// <remarks>
/// Individually trivial, which is exactly why they are worth measuring. They are called from layout and
/// navigation code rather than from one page, so a few milliseconds each shows up on every interaction,
/// and any of them landing far above the others points at a missing cache or an accidental full-table
/// read.
/// </remarks>
[BenchmarkCategory("Supporting")]
public class SupportingReadBenchmarks : ApiBenchmark
{
    private const int _labelPageSize = 50;
    private const int _transactionLogCount = 10;

    protected override async Task Prime()
    {
        await User_Get();
        await Currency_GetAll();
        await FinancialLabel_GetCount();
        await FinancialLabel_GetPage();
        await FinancialLabel_GetByAccount();
        await TransactionLog_Get();
    }

    [Benchmark(Description = "GET api/User/Get/{userId}", Baseline = true)]
    public Task<long> User_Get() => Get($"api/User/Get/{Scenario.UserId}");

    [Benchmark(Description = "GET api/Currency/GetAll")]
    public Task<long> Currency_GetAll() => Get("api/Currency/GetAll");

    [Benchmark(Description = "GET api/FinancialLabel/get-count")]
    public Task<long> FinancialLabel_GetCount() => Get("api/FinancialLabel/get-count");

    [Benchmark(Description = "GET api/FinancialLabel/get-by-index-and-count")]
    public Task<long> FinancialLabel_GetPage() =>
        Get($"api/FinancialLabel/get-by-index-and-count?index=0&count={_labelPageSize}");

    [Benchmark(Description = "GET api/FinancialLabel/get-by-account-id")]
    public Task<long> FinancialLabel_GetByAccount() =>
        Get($"api/FinancialLabel/get-by-account-id?accountId={Scenario.PrimaryCurrencyAccountId}");

    [Benchmark(Description = "GET api/TransactionLog/Get/{userId}")]
    public Task<long> TransactionLog_Get() =>
        Get($"api/TransactionLog/Get/{Scenario.UserId}?count={_transactionLogCount}");
}