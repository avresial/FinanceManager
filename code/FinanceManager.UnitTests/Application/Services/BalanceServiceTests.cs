using FinanceManager.Application.Services.Currencies;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

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
}