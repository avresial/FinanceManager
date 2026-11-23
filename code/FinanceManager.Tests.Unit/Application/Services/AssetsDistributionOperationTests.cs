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

    private void SetHoldings(params InvestmentHolding[] holdings) =>
        _transactions.Setup(x => x.GetHoldingsByUserAsOf(1, DateOnly.FromDateTime(_asOf), It.IsAny<CancellationToken>())).ReturnsAsync(holdings);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewOrRefresh_LoadsAndValuesInvestmentsOnce_ForBothDistributions(bool overview)
    {
        SetHoldings(Holding(11, "CSPX"), Holding(12, "VWRL"));
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [11] = 200m, [12] = -40m });

        var result = overview
            ? (await _dashboard.GetOverview(1, DefaultCurrency.PLN.Id, _asOf.AddDays(-7), _asOf, TestContext.Current.CancellationToken))!
            : await Refresh();

        var type = Assert.Single(result.AssetsPerType);
        Assert.Equal(("Stock", 160m), (type.Name, type.Value));
        var row = Assert.Single(result.AssetsPerAccount);
        Assert.Equal((null, "CSPX", 200m), (row.AccountId, row.Name, row.Value));
        _valuation.Verify(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()), Times.Once);
        _transactions.Verify(x => x.GetHoldingsByUserAsOf(1, DateOnly.FromDateTime(_asOf), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(80, 100)]
    [InlineData(100, 100)]
    [InlineData(120, 100)]
    public async Task Refresh_PreservesNegativeTypeTotals_AndShowsOnlyPositiveRows(int positive, int negative)
    {
        SetHoldings(Holding(11, "AAA"), Holding(12, "BBB"), Holding(13, "CCC"));
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [11] = positive - 10, [12] = -negative, [13] = 10 });

        var result = await Refresh();

        if (positive > negative)
            Assert.Equal(positive - negative, Assert.Single(result.AssetsPerType).Value);
        else
            Assert.Empty(result.AssetsPerType);
        Assert.Equal(new[] { ("AAA", (decimal)positive - 10), ("CCC", 10m) }, result.AssetsPerAccount.Select(x => (x.Name, x.Value)));
        Assert.All(result.AssetsPerAccount, x => Assert.Null(x.AccountId));
    }

    [Fact]
    public async Task Refresh_ShowsTheExchangeOnlyWhenHeldListingsShareATicker()
    {
        SetHoldings(Holding(11, "CSPX", "XLON"), Holding(12, "CSPX", "XAMS"), Holding(13, "VWRL", "XLON"));
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [11] = 10m, [12] = 20m, [13] = 30m });

        var result = await Refresh();

        Assert.Equal(["CSPX (XLON)", "CSPX (XAMS)", "VWRL"], result.AssetsPerAccount.Select(x => x.Name));
    }

    [Fact]
    public async Task Refresh_NamesAListingThatHasNoHoldingRowByItsId()
    {
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [42] = 10m });

        Assert.Equal("Listing 42", Assert.Single((await Refresh()).AssetsPerAccount).Name);
    }

    [Fact]
    public async Task Refresh_ReturnsNothing_WhenThereAreNoHoldings()
    {
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal>());

        var result = await Refresh();

        Assert.Empty(result.AssetsPerType);
        Assert.Empty(result.AssetsPerAccount);
        _transactions.Verify(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_IsolatesUserCurrencyAndExactTimestamp_InEitherCallOrder()
    {
        _transactions.Setup(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long user, DateOnly _, CancellationToken _) => [Holding(user, $"USER{user}")]);
        _valuation.Setup(x => x.GetUserValueByListingAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int user, Currency currency, DateTime end, CancellationToken ct) =>
                new Dictionary<long, decimal> { [user] = user * 1000m + currency.Id + end.Ticks % 100 });
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
            _valuation.Verify(x => x.GetUserValueByListingAsync(request.User, request.Currency, request.End, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
        Assert.Equal(8, _valuation.Invocations.Count);
    }

    [Fact]
    public async Task Refresh_RetriesFailure_AndObservesLaterHoldingAndValuationChanges()
    {
        var holdings = new List<InvestmentHolding> { Holding(11, "BEFORE") };
        _transactions.Setup(x => x.GetHoldingsByUserAsOf(1, DateOnly.FromDateTime(_asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [.. holdings]);
        var values = new Dictionary<long, decimal> { [11] = 100m };
        _valuation.SetupSequence(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("valuation failed"))
            .ReturnsAsync(values)
            .ReturnsAsync(values);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Refresh());
        var initial = await Refresh();
        holdings.Add(Holding(12, "NEW"));
        values[11] = 250m;
        values[12] = 50m;
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
        SetHoldings(Holding(11, "CSPX"));
        var firstValuation = new TaskCompletionSource<IReadOnlyDictionary<long, decimal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondValuation = new TaskCompletionSource<IReadOnlyDictionary<long, decimal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _valuation.SetupSequence(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .Returns(firstValuation.Task)
            .Returns(secondValuation.Task);

        var first = Refresh();
        Assert.Single(_valuation.Invocations);
        var second = Refresh();
        Assert.Equal(2, _valuation.Invocations.Count);
        secondValuation.SetResult(new Dictionary<long, decimal> { [11] = 200m });
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstValuation.SetResult(new Dictionary<long, decimal> { [11] = 100m });
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
        SetHoldings(Holding(11, "CSPX"));
        _valuation.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [11] = 150m });

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