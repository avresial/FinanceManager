using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class NetWorthServiceTests
{
    private readonly NetWorthService _netWorthService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();

    public NetWorthServiceTests()
    {
        _bondDetailsRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<BondDetails>());

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<InvestmentAccount>());

        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>());

        _netWorthService = new NetWorthService(_financialAccountRepositoryMock.Object, _bondDetailsRepositoryMock.Object, _investmentValuationServiceMock.Object);
    }

    [Fact]
    public async Task GetNetWorth_NoAccounts_ReturnsZero()
    {
        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, DateTime.UtcNow);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetNetWorth_CurrencyAccount_IncludesBalance()
    {
        var date = DateTime.UtcNow;
        var account = new CurrencyAccount(1, 1, "Cash", AccountLabel.Other);
        account.Add(new CurrencyAccountEntry(1, 1, date.AddDays(-1), 0, 100m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, date);

        Assert.Equal(100m, result);
    }

    [Fact]
    public async Task GetNetWorth_InvestmentAccount_IncludesValuation()
    {
        var date = DateTime.UtcNow;
        var account = new InvestmentAccount(1, 5, "Investments");

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(5, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, date);

        Assert.Equal(250m, result);
    }

    [Fact]
    public async Task GetNetWorthSeries_InvestmentAccount_UsesDailyValuationSeries()
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-2);
        var account = new InvestmentAccount(1, 5, "Investments");

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var series = new Dictionary<DateTime, decimal>
        {
            [start] = 10m,
            [start.AddDays(1)] = 20m,
            [end] = 30m
        };
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(5)), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>> { [5] = series });

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(10m, result[start]);
        Assert.Equal(30m, result[end]);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_FetchesAllInvestmentAccountsInOneBatchedCall()
    {
        // The per-account fan-out is replaced by a single batched valuation call that carries every
        // investment account id. This keeps the shared scoped AppDbContext single-threaded (one query
        // for all accounts) instead of one round-trip per account.
        const int userId = 1;
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        List<InvestmentAccount> investmentAccounts =
        [
            new(userId, 1, "Investments A"),
            new(userId, 2, "Investments B"),
            new(userId, 3, "Investments C")
        ];

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(investmentAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        List<int>? capturedIds = null;
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, Currency _, DateTime _, DateTime _, CancellationToken _) => capturedIds = ids.ToList())
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>());

        await _netWorthService.GetNetWorth(userId, DefaultCurrency.PLN, start, end);

        // Exactly one batched call, carrying all three account ids.
        _investmentValuationServiceMock.Verify(
            x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _investmentValuationServiceMock.Verify(
            x => x.GetAccountValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.NotNull(capturedIds);
        Assert.Equal(new[] { 1, 2, 3 }, capturedIds!.OrderBy(x => x));
    }
}