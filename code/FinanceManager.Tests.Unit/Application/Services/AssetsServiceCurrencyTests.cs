using FinanceManager.Application.FinancialAccounts.Currencies.Assets;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsServiceCurrencyTests
{
    private const int _plnId = 0;
    private const int _eurId = 2;
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Currency _pln = new(_plnId, "PLN", "zł");

    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly AssetsServiceCurrency _service;

    public AssetsServiceCurrencyTests()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(() => new[] { _pln, new Currency(_eurId, "EUR", "€") }.ToAsyncEnumerable());
        _service = new(_ledger, currencies.Object);
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_IsTrue_WhenACurrencyNetsPositive()
    {
        _ledger.Add(_plnId, DateTime.UtcNow.AddDays(-1), 10m);

        Assert.True(await _service.IsAnyAccountWithAssets(1));
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_IsFalse_WhenTheOnlyBalanceIsNegative()
    {
        _ledger.Add(_plnId, DateTime.UtcNow.AddDays(-1), -10m);

        Assert.False(await _service.IsAnyAccountWithAssets(1));
    }

    [Fact]
    public async Task GetEndAssetsDistribution_PoolsEachCurrencyAndNamesItByCode()
    {
        _ledger.Add(_plnId, _end.AddDays(-3), 10m).Add(_plnId, _end.AddDays(-2), 5m).Add(_eurId, _end.AddDays(-1), 7m);

        var distribution = await _service.GetEndAssetsDistribution(1, _pln, _end, TestContext.Current.CancellationToken);

        Assert.Equal([("PLN", 15m), ("EUR", 7m)], distribution.PerAccount.Select(x => (x.Name, x.Value)));
        Assert.All(distribution.PerAccount, x => Assert.Null(x.AccountId));
        var total = Assert.Single(distribution.PerType);
        Assert.Equal(("Cash", 22m), (total.Name, total.Value));
    }

    [Fact]
    public async Task GetEndAssetsDistribution_NetsPositiveAgainstNegativeWithinOneCurrency()
    {
        _ledger.Add(_plnId, _end.AddDays(-3), 100m).Add(_plnId, _end.AddDays(-2), -30m);

        var distribution = await _service.GetEndAssetsDistribution(1, _pln, _end, TestContext.Current.CancellationToken);

        Assert.Equal(70m, Assert.Single(distribution.PerAccount).Value);
    }

    [Fact]
    public async Task GetEndAssetsDistribution_LeavesNegativeCurrenciesToLiabilities()
    {
        _ledger.Add(_plnId, _end.AddDays(-3), 40m).Add(_eurId, _end.AddDays(-3), -25m);

        var distribution = await _service.GetEndAssetsDistribution(1, _pln, _end, TestContext.Current.CancellationToken);

        Assert.Equal("PLN", Assert.Single(distribution.PerAccount).Name);
        Assert.Equal(40m, Assert.Single(distribution.PerType).Value);
    }

    [Fact]
    public async Task GetEndAssetsDistribution_FallsBackToTheCurrencyIdForAnUnknownCurrency()
    {
        _ledger.Add(77, _end.AddDays(-1), 1m);

        var distribution = await _service.GetEndAssetsDistribution(1, _pln, _end, TestContext.Current.CancellationToken);

        Assert.Equal("Currency 77", Assert.Single(distribution.PerAccount).Name);
    }

    [Fact]
    public async Task GetEndAssetsPerAccountAndPerType_MatchTheDistribution()
    {
        _ledger.Add(_plnId, _end.AddDays(-1), 15m);

        var perAccount = await _service.GetEndAssetsPerAccount(1, _pln, _end).ToListAsync(TestContext.Current.CancellationToken);
        var perType = await _service.GetEndAssetsPerType(1, _pln, _end).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("PLN", 15m), (perAccount[0].Name, perAccount[0].Value));
        Assert.Equal(("Cash", 15m), (perType[0].Name, perType[0].Value));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsEveryDayWithTheCarriedForwardBalance()
    {
        _ledger.Add(_plnId, _start.AddDays(-5), 10m).Add(_plnId, _start.AddDays(2).AddHours(9), 20m);

        var series = await _service.GetAssetsTimeSeries(1, _pln, _start, _end);

        Assert.Equal([10m, 10m, 30m, 30m, 30m], series.Select(x => x.Value));
        Assert.Equal(_start, series[0].DateTime);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ShowsZeroWhenTheBalanceTurnsNegativeInsteadOfYesterdaysValue()
    {
        _ledger.Add(_plnId, _start.AddDays(-1), 100m).Add(_plnId, _start.AddDays(2).AddHours(9), -150m);

        var series = await _service.GetAssetsTimeSeries(1, _pln, _start, _end);

        Assert.Equal([100m, 100m, 0m, 0m, 0m], series.Select(x => x.Value));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_IsEmpty_WhenTheUserHasNoCashEntries()
    {
        Assert.Empty(await _service.GetAssetsTimeSeries(1, _pln, _start, _end));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ByInvestmentType_OnlyAnswersForCash()
    {
        _ledger.Add(_plnId, _start, 10m);

        Assert.NotEmpty(await _service.GetAssetsTimeSeries(1, _pln, _start, _end, InvestmentType.Cash));
        Assert.Empty(await _service.GetAssetsTimeSeries(1, _pln, _start, _end, InvestmentType.Bond));
    }
}