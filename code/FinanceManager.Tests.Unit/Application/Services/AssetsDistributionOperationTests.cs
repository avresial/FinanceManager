using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.FinancialAccounts.Investments.Assets;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
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
    private readonly Mock<IFinancialAccountRepository> _accounts = new();
    private readonly Mock<IInvestmentValuationService> _valuation = new();
    private readonly AssetsServiceInvestment _investment;
    private readonly DashboardQueryService _dashboard;

    public AssetsDistributionOperationTests()
    {
        _investment = new AssetsServiceInvestment(_accounts.Object,
            Mock.Of<IAccountRepository<InvestmentAccount>>(), _valuation.Object,
            Mock.Of<IInvestmentTransactionRepository>(), Mock.Of<IInvestmentPriceProvider>(),
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewOrRefresh_LoadsAndValuesInvestmentsOnce_ForBothDistributions(bool overview)
    {
        var accounts = new[] { new InvestmentAccount(1, 7, "First"), new InvestmentAccount(1, 3, "Second") };
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf))
            .Returns(accounts.ToAsyncEnumerable());
        _valuation.Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [7] = 200m, [3] = -40m });

        var result = overview
            ? (await _dashboard.GetOverview(1, DefaultCurrency.PLN.Id, _asOf.AddDays(-7), _asOf, TestContext.Current.CancellationToken))!
            : await Refresh();

        Assert.Equal(160m, Assert.Single(result.AssetsPerType).Value);
        var row = Assert.Single(result.AssetsPerAccount);
        Assert.Equal((7, "First", 200m), (row.AccountId, row.Name, row.Value));
        _accounts.Verify(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf), Times.Once);
        _valuation.Verify(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 7, 3 })), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(80, 100)]
    [InlineData(100, 100)]
    [InlineData(120, 100)]
    public async Task Refresh_PreservesNegativeTypeTotals_PositiveRowsAndAccountOrder(int positive, int negative)
    {
        var accounts = new[]
        {
            new InvestmentAccount(1, 7, "Duplicate"), new InvestmentAccount(1, 3, "Negative"),
            new InvestmentAccount(1, 9, "Zero"), new InvestmentAccount(1, 2, "Missing"),
            new InvestmentAccount(1, 4, "Duplicate"),
        };
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf))
            .Returns(accounts.ToAsyncEnumerable());
        _valuation.Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [7] = positive - 10, [3] = -negative, [9] = 0, [4] = 10 });

        var result = await Refresh();

        if (positive > negative)
            Assert.Equal(positive - negative, Assert.Single(result.AssetsPerType).Value);
        else
            Assert.Empty(result.AssetsPerType);
        Assert.Equal(new[] { (7, "Duplicate", (decimal)positive - 10), (4, "Duplicate", 10m) },
            result.AssetsPerAccount.Select(x => (x.AccountId!.Value, x.Name, x.Value)));
    }

    [Fact]
    public async Task Refresh_IsolatesUserCurrencyAndExactTimestamp_InEitherCallOrder()
    {
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns((int user, DateTime start, DateTime end, bool meta) =>
                new[] { new InvestmentAccount(user, user, $"User {user}") }.ToAsyncEnumerable());
        _valuation.Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, Currency currency, DateTime end, CancellationToken ct) =>
                new Dictionary<int, decimal> { [ids.Single()] = ids.Single() * 1000m + currency.Id + end.Ticks % 100 });
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
            Assert.Equal(expected, Assert.Single(result.AssetsPerAccount).Value);
            _accounts.Verify(x => x.GetAccounts<InvestmentAccount>(request.User, request.End.AddMinutes(-1), request.End), Times.AtLeastOnce);
        }
        Assert.Equal(8, _valuation.Invocations.Count);
        Assert.Equal(8, _accounts.Invocations.Count);
    }

    [Fact]
    public async Task Refresh_RetriesFailure_AndObservesLaterAccountAndValuationChanges()
    {
        var account = new InvestmentAccount(1, 7, "Before");
        var accounts = new List<InvestmentAccount> { account };
        var values = new Dictionary<int, decimal> { [7] = 100m };
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf))
            .Returns(() => accounts.ToAsyncEnumerable());
        _valuation.SetupSequence(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("valuation failed"))
            .ReturnsAsync(values)
            .ReturnsAsync(values);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Refresh());
        var initial = await Refresh();
        account.Name = "After";
        accounts.Add(new InvestmentAccount(1, 8, "New"));
        values[7] = 250m;
        values[8] = 50m;
        var next = await Refresh();

        var initialRow = Assert.Single(initial.AssetsPerAccount);
        Assert.Equal(("Before", 100m), (initialRow.Name, initialRow.Value));
        Assert.Equal(100m, Assert.Single(initial.AssetsPerType).Value);
        Assert.Equal(new[] { (7, "After", 250m), (8, "New", 50m) },
            next.AssetsPerAccount.Select(x => (x.AccountId!.Value, x.Name, x.Value)));
        Assert.Equal(300m, Assert.Single(next.AssetsPerType).Value);
        _accounts.Verify(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf), Times.Exactly(3));
        Assert.Equal(3, _valuation.Invocations.Count);
    }

    [Fact]
    public async Task OverlappingRefreshes_KeepIndependentValuesAndCopiedAccountMetadata()
    {
        var account = new InvestmentAccount(1, 7, "Before");
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf))
            .Returns(new[] { account }.ToAsyncEnumerable());
        var firstValuation = new TaskCompletionSource<IReadOnlyDictionary<int, decimal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondValuation = new TaskCompletionSource<IReadOnlyDictionary<int, decimal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _valuation.SetupSequence(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .Returns(firstValuation.Task)
            .Returns(secondValuation.Task);

        var first = Refresh();
        Assert.Single(_valuation.Invocations);
        account.Name = "After";
        var second = Refresh();
        Assert.Equal(2, _valuation.Invocations.Count);
        secondValuation.SetResult(new Dictionary<int, decimal> { [7] = 200m });
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstValuation.SetResult(new Dictionary<int, decimal> { [7] = 100m });
        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(("Before", 100m), (Assert.Single(firstResult.AssetsPerAccount).Name, Assert.Single(firstResult.AssetsPerType).Value));
        Assert.Equal(("After", 200m), (Assert.Single(secondResult.AssetsPerAccount).Name, Assert.Single(secondResult.AssetsPerType).Value));
        secondResult.AssetsPerAccount[0].Value = 999m;
        Assert.Equal(100m, Assert.Single(firstResult.AssetsPerAccount).Value);
        Assert.Equal(2, _valuation.Invocations.Count);
        Assert.Equal(2, _accounts.Invocations.Count);
    }

    [Fact]
    public async Task StandaloneCalls_InEitherOrder_AgreeWithPairedRead()
    {
        _accounts.Setup(x => x.GetAccounts<InvestmentAccount>(1, _asOf.AddMinutes(-1), _asOf))
            .Returns(new[] { new InvestmentAccount(1, 7, "Investment") }.ToAsyncEnumerable());
        _valuation.Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [7] = 150m });

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