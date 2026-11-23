using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.FinancialAccounts.Investments.Assets;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsDistributionOperationTests
{
    private static readonly DateTime _asOf = new(2026, 5, 12, 10, 30, 0, DateTimeKind.Utc);
    private readonly Mock<IInvestmentValuationService> _valuation = new();
    private readonly Mock<IInvestmentTransactionRepository> _transactions = new();
    private readonly AssetsServiceInvestment _investment;
    private readonly DashboardQueryService _dashboard;

    // A conventional constructor configures shared mocks before composing the services under test.
    public AssetsDistributionOperationTests()
    {
        _transactions.Setup(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _investment = new AssetsServiceInvestment(Mock.Of<IFinancialAccountRepository>(),
            Mock.Of<IAccountRepository<InvestmentAccount>>(), _valuation.Object,
            _transactions.Object, Mock.Of<IInvestmentPriceProvider>(),
            Mock.Of<ICurrencyExchangeService>());
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(x => x.GetCurrency(DefaultCurrency.PLN.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        currencies.Setup(x => x.GetCurrency(DefaultCurrency.USD.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.USD);
        var netWorth = new Mock<INetWorthService>();
        netWorth.Setup(x => x.GetNetWorth(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());
        var liabilities = new Mock<ILiabilitiesService>();
        liabilities.Setup(x => x.GetEndLiabilitiesPerType(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        liabilities.Setup(x => x.GetEndLiabilitiesPerAccount(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        _dashboard = new DashboardQueryService(netWorth.Object, Mock.Of<ILabelsValueService>(),
            Mock.Of<IBalanceService>(), new AssetsService([_investment]), liabilities.Object,
            Mock.Of<IExpenseDistributionService>(), currencies.Object);
    }

    private static InvestmentHolding Holding(long listingId, string ticker, string exchange = "XLON") =>
        new(listingId, ticker, exchange, AssetType.ETF, 1m);

    private static InvestmentHoldingValue Valued(InvestmentHolding holding, decimal value) => new(holding, value);

    private void SetValues(params InvestmentHoldingValue[] values) =>
        _valuation.Setup(x => x.GetUserHoldingValuesAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>())).ReturnsAsync(values);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewOrRefresh_LoadsAndValuesInvestmentsOnce_ForBothDistributions(bool overview)
    {
        SetValues(Valued(Holding(11, "CSPX"), 200m), Valued(Holding(12, "VWRL"), -40m));

        var result = overview
            ? (await _dashboard.GetOverview(1, DefaultCurrency.PLN.Id, _asOf.AddDays(-7), _asOf, TestContext.Current.CancellationToken))!
            : await Refresh();

        var type = Assert.Single(result.AssetsPerType);
        Assert.Equal(("Stock", 160m), (type.Name, type.Value));
        var row = Assert.Single(result.AssetsPerAccount);
        Assert.Equal((null, "CSPX", 200m, "Stock", null), (row.AccountId, row.Name, row.Value, row.Kind, row.CurrencyCode));
        _valuation.Verify(x => x.GetUserHoldingValuesAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()), Times.Once);
        // The holdings are read once, inside the valuation: naming the rows needs no second pass over the trades.
        _transactions.Verify(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(80, 100)]
    [InlineData(100, 100)]
    [InlineData(120, 100)]
    public async Task Refresh_PreservesNegativeTypeTotals_AndShowsOnlyPositiveRows(int positive, int negative)
    {
        SetValues(Valued(Holding(11, "AAA"), positive - 10), Valued(Holding(12, "BBB"), -negative), Valued(Holding(13, "CCC"), 10));

        var result = await Refresh();

        if (positive > negative)
            Assert.Equal(positive - negative, Assert.Single(result.AssetsPerType).Value);
        else
            Assert.Empty(result.AssetsPerType);
        Assert.Equal(new[] { ("AAA", (decimal)positive - 10), ("CCC", 10m) }, result.AssetsPerAccount.Select(x => (x.Name, x.Value)));
        Assert.All(result.AssetsPerAccount, x => Assert.Null(x.AccountId));
    }

    [Fact]
    public async Task Refresh_ShowsTheExchangeOnlyWhenShownListingsShareATicker()
    {
        SetValues(Valued(Holding(11, "CSPX", "XLON"), 10m), Valued(Holding(12, "CSPX", "XAMS"), 20m), Valued(Holding(13, "VWRL", "XLON"), 30m));

        var result = await Refresh();

        Assert.Equal(["CSPX (XLON)", "CSPX (XAMS)", "VWRL"], result.AssetsPerAccount.Select(x => x.Name));
    }

    [Fact]
    public async Task Refresh_DoesNotForceAnExchangeOntoTheOnlyShownRow_WhenTheSameTickerIsOversold()
    {
        SetValues(Valued(Holding(11, "CSPX", "XLON"), 10m), Valued(Holding(12, "CSPX", "XAMS"), -20m));

        Assert.Equal("CSPX", Assert.Single((await Refresh()).AssetsPerAccount).Name);
    }

    [Fact]
    public async Task Refresh_OmitsAnEmptyExchangeFromASharedTicker()
    {
        SetValues(Valued(Holding(11, "CSPX", ""), 10m), Valued(Holding(12, "CSPX", "XAMS"), 20m));

        Assert.Equal(["CSPX", "CSPX (XAMS)"], (await Refresh()).AssetsPerAccount.Select(x => x.Name));
    }

    [Fact]
    public async Task Refresh_ReturnsNothing_WhenThereAreNoHoldings()
    {
        SetValues();

        var result = await Refresh();

        Assert.Empty(result.AssetsPerType);
        Assert.Empty(result.AssetsPerAccount);
    }

    [Fact]
    public async Task Refresh_IsolatesUserCurrencyAndExactTimestamp_InEitherCallOrder()
    {
        _valuation.Setup(x => x.GetUserHoldingValuesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int user, Currency currency, DateTime end, CancellationToken ct) =>
                [Valued(Holding(user, $"USER{user}"), user * 1000m + currency.Id + end.Ticks % 100)]);
        var requests = new[]
        {
            (User: 1, Currency: DefaultCurrency.PLN, End: _asOf),
            (User: 2, Currency: DefaultCurrency.PLN, End: _asOf),
            (User: 1, Currency: DefaultCurrency.USD, End: _asOf),
            (User: 1, Currency: DefaultCurrency.PLN, End: _asOf.AddTicks(1)),
        };
        foreach (var request in requests.Concat(requests.Reverse()))
        {
            var result = await Refresh(request.User, request.Currency, request.End);
            var expected = request.User * 1000m + request.Currency.Id + request.End.Ticks % 100;
            Assert.Equal(expected, Assert.Single(result.AssetsPerType).Value);
            var row = Assert.Single(result.AssetsPerAccount);
            Assert.Equal(($"USER{request.User}", expected), (row.Name, row.Value));
            _valuation.Verify(x => x.GetUserHoldingValuesAsync(request.User, request.Currency, request.End, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
        Assert.Equal(8, _valuation.Invocations.Count);
    }

    [Fact]
    public async Task Refresh_RetriesFailure_AndObservesLaterHoldingAndValuationChanges()
    {
        var values = new List<InvestmentHoldingValue> { Valued(Holding(11, "BEFORE"), 100m) };
        _valuation.SetupSequence(x => x.GetUserHoldingValuesAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("valuation failed"))
            .ReturnsAsync(() => [.. values])
            .ReturnsAsync(() => [.. values]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Refresh());
        var initial = await Refresh();
        values[0] = Valued(Holding(11, "BEFORE"), 250m);
        values.Add(Valued(Holding(12, "NEW"), 50m));
        var next = await Refresh();

        var initialRow = Assert.Single(initial.AssetsPerAccount);
        Assert.Equal(("BEFORE", 100m), (initialRow.Name, initialRow.Value));
        Assert.Equal(100m, Assert.Single(initial.AssetsPerType).Value);
        Assert.Equal(new[] { ("BEFORE", 250m), ("NEW", 50m) }, next.AssetsPerAccount.Select(x => (x.Name, x.Value)));
        Assert.Equal(300m, Assert.Single(next.AssetsPerType).Value);
        Assert.Equal(3, _valuation.Invocations.Count);
    }

    [Fact]
    public async Task OverlappingRefreshes_KeepIndependentValues()
    {
        var firstValuation = new TaskCompletionSource<IReadOnlyList<InvestmentHoldingValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondValuation = new TaskCompletionSource<IReadOnlyList<InvestmentHoldingValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _valuation.SetupSequence(x => x.GetUserHoldingValuesAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .Returns(firstValuation.Task)
            .Returns(secondValuation.Task);

        var first = Refresh();
        Assert.Single(_valuation.Invocations);
        var second = Refresh();
        Assert.Equal(2, _valuation.Invocations.Count);
        secondValuation.SetResult([Valued(Holding(11, "CSPX"), 200m)]);
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstValuation.SetResult([Valued(Holding(11, "CSPX"), 100m)]);
        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(100m, Assert.Single(firstResult.AssetsPerType).Value);
        Assert.Equal(200m, Assert.Single(secondResult.AssetsPerType).Value);
        secondResult.AssetsPerAccount[0].Value = 999m;
        Assert.Equal(100m, Assert.Single(firstResult.AssetsPerAccount).Value);
        Assert.Equal(2, _valuation.Invocations.Count);
    }

    [Fact]
    public async Task StandaloneCalls_InEitherOrder_AgreeWithPairedRead()
    {
        SetValues(Valued(Holding(11, "CSPX"), 150m));

        var accountFirst = await _investment.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, _asOf).ToListAsync(TestContext.Current.CancellationToken);
        var typeSecond = await _investment.GetEndAssetsPerType(1, DefaultCurrency.PLN, _asOf).ToListAsync(TestContext.Current.CancellationToken);
        var paired = await Refresh();
        var typeFirst = await _investment.GetEndAssetsPerType(1, DefaultCurrency.PLN, _asOf).ToListAsync(TestContext.Current.CancellationToken);
        var accountSecond = await _investment.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, _asOf).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(typeFirst.Select(x => (x.Name, x.Value)), paired.AssetsPerType.Select(x => (x.Name, x.Value)));
        Assert.Equal(typeSecond.Select(x => (x.Name, x.Value)), paired.AssetsPerType.Select(x => (x.Name, x.Value)));
        Assert.Equal(accountFirst.Select(x => (x.AccountId, x.Name, x.Value)), paired.AssetsPerAccount.Select(x => (x.AccountId, x.Name, x.Value)));
        Assert.Equal(accountSecond.Select(x => (x.AccountId, x.Name, x.Value)), paired.AssetsPerAccount.Select(x => (x.AccountId, x.Name, x.Value)));
        Assert.Equal(5, _valuation.Invocations.Count);
    }

    private async Task<DashboardOverviewDto> Refresh(int userId = 1, Currency? currency = null, DateTime? asOf = null)
    {
        return (await _dashboard.RefreshAssetsDistribution(new DashboardOverviewDto
        {
            UserId = userId,
            CurrencyId = (currency ?? DefaultCurrency.PLN).Id,
            EndDate = asOf ?? _asOf,
        }, TestContext.Current.CancellationToken))!;
    }
}