using FinanceManager.Application.FinancialAccounts.Currencies.Balance;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyBalanceServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly CurrencyBalanceService _balanceService;

    public CurrencyBalanceServiceTests()
    {
        _balanceService = new CurrencyBalanceService(_financialAccountRepositoryMock.Object);
    }

    [Fact]
    public async Task GetInflow_ReturnsInflowTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 100, 100));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetInflow(userId, DefaultCurrency.PLN, _startDate, _endDate);

        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value > 0);
    }

    [Fact]
    public async Task GetInflow_WithAccountIds_FiltersAccounts()
    {
        var userId = 1;
        var firstAccount = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        firstAccount.Add(new CurrencyAccountEntry(1, 1, _startDate, 100, 100));

        var secondAccount = new CurrencyAccount(userId, 2, "Currency Account 2", AccountLabel.Cash);
        secondAccount.Add(new CurrencyAccountEntry(2, 1, _startDate, 30, 30));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { firstAccount, secondAccount }.ToAsyncEnumerable());

        var result = await _balanceService.GetInflow(userId, DefaultCurrency.PLN, _startDate, _startDate, [2]);

        Assert.NotEmpty(result);
        Assert.Single(result);
        Assert.Equal(30, result.First().Value);
    }

    [Fact]
    public async Task GetOutflow_ReturnsOutflowTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, -50, -50));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetOutflow(userId, DefaultCurrency.PLN, _startDate, _endDate);

        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value < 0);
    }

    [Fact]
    public async Task GetClosingBalance_ReturnsDailyClosingBalanceTimeSeries()
    {
        var userId = 1;
        var laterDate = _startDate.AddDays(2);
        var account = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 1, laterDate, 100, 40),
                new CurrencyAccountEntry(1, 2, _startDate, 60, 60)
            ],
            AccountLabel.Cash);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, _startDate, laterDate);

        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(60, result.Single(x => x.DateTime == _startDate).Value);
        Assert.Equal(60, result.Single(x => x.DateTime == _startDate.AddDays(1)).Value);
        Assert.Equal(100, result.Single(x => x.DateTime == laterDate).Value);
        _financialAccountRepositoryMock.Verify(
            repo => repo.GetAccount<CurrencyAccount>(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task GetClosingBalance_ForLongRange_UsesLastValuePerBucket()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 1);
        DateTime endDate = new(2024, 4, 5);
        var account = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 4, new DateTime(2024, 4, 1), 40, 10),
                new CurrencyAccountEntry(1, 3, new DateTime(2024, 3, 1), 30, 10),
                new CurrencyAccountEntry(1, 2, new DateTime(2024, 2, 1), 20, 10),
                new CurrencyAccountEntry(1, 1, new DateTime(2024, 1, 1), 10, 10)
            ],
            AccountLabel.Cash);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(4, result.Count);
        Assert.Equal(10, result.Single(x => x.DateTime == new DateTime(2024, 1, 1)).Value);
        Assert.Equal(20, result.Single(x => x.DateTime == new DateTime(2024, 2, 1)).Value);
        Assert.Equal(30, result.Single(x => x.DateTime == new DateTime(2024, 3, 1)).Value);
        Assert.Equal(40, result.Single(x => x.DateTime == new DateTime(2024, 4, 1)).Value);
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsNetTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 50, 50));
        account.Add(new CurrencyAccountEntry(1, 2, _startDate, 10, -40));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetNetCashFlow(userId, DefaultCurrency.PLN, _startDate, _startDate);

        Assert.NotEmpty(result);
        Assert.Single(result);
        Assert.Equal(10, result.First().Value);
    }

    [Fact]
    public async Task GetClosingBalance_WithSparseEmptyDaysAndNextOlderEntry_CarriesForwardClosingBalanceAcrossGaps()
    {
        var userId = 1;
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 7);
        var nextOlder = new CurrencyAccountEntry(1, 0, start.AddDays(-1), 100, 100);
        var account = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 1, start.AddDays(2), 150, 50),
                new CurrencyAccountEntry(1, 2, start.AddDays(5), 180, 30)
            ],
            AccountLabel.Cash,
            nextOlderEntry: nextOlder);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(7, result.Count);
        Assert.Equal(100, result.Single(x => x.DateTime == start).Value);
        Assert.Equal(100, result.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(150, result.Single(x => x.DateTime == start.AddDays(2)).Value);
        Assert.Equal(150, result.Single(x => x.DateTime == start.AddDays(3)).Value);
        Assert.Equal(150, result.Single(x => x.DateTime == start.AddDays(4)).Value);
        Assert.Equal(180, result.Single(x => x.DateTime == start.AddDays(5)).Value);
        Assert.Equal(180, result.Single(x => x.DateTime == start.AddDays(6)).Value);
    }

    [Fact]
    public async Task GetClosingBalance_WithUnorderedAndMultipleSameDayEntries_PicksLatestChronologicalValue()
    {
        var userId = 1;
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        var account = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 1, start.AddHours(18), 120, 20),
                new CurrencyAccountEntry(1, 2, start.AddDays(1).AddHours(10), 160, 40),
                new CurrencyAccountEntry(1, 3, start.AddHours(9), 100, 100),
                new CurrencyAccountEntry(1, 4, start.AddDays(1).AddHours(20), 190, 30),
                new CurrencyAccountEntry(1, 5, start.AddDays(1).AddHours(14), 170, 10)
            ],
            AccountLabel.Cash);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(2, result.Count);
        Assert.Equal(120, result.Single(x => x.DateTime == start).Value);
        Assert.Equal(190, result.Single(x => x.DateTime == start.AddDays(1)).Value);
    }

    [Fact]
    public async Task GetClosingBalance_WithAccountIds_FiltersAccountsAndAggregates()
    {
        var userId = 1;
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        var firstAccount = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 1, start, 100, 100)
            ],
            AccountLabel.Cash);

        var secondAccount = new CurrencyAccount(
            userId,
            2,
            "Currency Account 2",
            [
                new CurrencyAccountEntry(2, 1, start, 50, 50)
            ],
            AccountLabel.Cash);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { firstAccount, secondAccount }.ToAsyncEnumerable());

        var filteredResult = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, start, end, [2]);
        var allResult = await _balanceService.GetClosingBalance(userId, DefaultCurrency.PLN, start, end, [1, 2]);

        Assert.Equal(2, filteredResult.Count);
        Assert.Equal(50, filteredResult.Single(x => x.DateTime == start).Value);
        Assert.Equal(50, filteredResult.Single(x => x.DateTime == start.AddDays(1)).Value);

        Assert.Equal(2, allResult.Count);
        Assert.Equal(150, allResult.Single(x => x.DateTime == start).Value);
        Assert.Equal(150, allResult.Single(x => x.DateTime == start.AddDays(1)).Value);
    }

    [Fact]
    public async Task FlowAggregation_IgnoresOutOfRangeEntriesAndZeroFillsRange()
    {
        var userId = 1;
        var start = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 5);
        var account = new CurrencyAccount(
            userId,
            1,
            "Currency Account 1",
            [
                new CurrencyAccountEntry(1, 1, start.AddDays(-2), 500, 500),
                new CurrencyAccountEntry(1, 2, start.AddDays(1), 100, 100),
                new CurrencyAccountEntry(1, 3, start.AddDays(1).AddHours(2), 70, -30),
                new CurrencyAccountEntry(1, 4, start.AddDays(10), 300, 230)
            ],
            AccountLabel.Cash);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var inflow = await _balanceService.GetInflow(userId, DefaultCurrency.PLN, start, end);
        var outflow = await _balanceService.GetOutflow(userId, DefaultCurrency.PLN, start, end);
        var netFlow = await _balanceService.GetNetCashFlow(userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(4, inflow.Count);
        Assert.Equal(0, inflow.Single(x => x.DateTime == start).Value);
        Assert.Equal(100, inflow.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(0, inflow.Single(x => x.DateTime == start.AddDays(2)).Value);
        Assert.Equal(0, inflow.Single(x => x.DateTime == start.AddDays(3)).Value);

        Assert.Equal(4, outflow.Count);
        Assert.Equal(0, outflow.Single(x => x.DateTime == start).Value);
        Assert.Equal(-30, outflow.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(0, outflow.Single(x => x.DateTime == start.AddDays(2)).Value);
        Assert.Equal(0, outflow.Single(x => x.DateTime == start.AddDays(3)).Value);

        Assert.Equal(4, netFlow.Count);
        Assert.Equal(0, netFlow.Single(x => x.DateTime == start).Value);
        Assert.Equal(70, netFlow.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(0, netFlow.Single(x => x.DateTime == start.AddDays(2)).Value);
        Assert.Equal(0, netFlow.Single(x => x.DateTime == start.AddDays(3)).Value);
    }

    [Fact]
    public async Task GetOutflow_WithAccountIds_FiltersAccounts()
    {
        var userId = 1;
        var start = new DateTime(2024, 1, 1);
        var firstAccount = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        firstAccount.Add(new CurrencyAccountEntry(1, 1, start, -100, -100));

        var secondAccount = new CurrencyAccount(userId, 2, "Currency Account 2", AccountLabel.Cash);
        secondAccount.Add(new CurrencyAccountEntry(2, 1, start, -45, -45));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { firstAccount, secondAccount }.ToAsyncEnumerable());

        var result = await _balanceService.GetOutflow(userId, DefaultCurrency.PLN, start, start, [2]);

        Assert.Single(result);
        Assert.Equal(-45, result.First().Value);
    }
}