using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class BalanceServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly BalanceService _balanceService;

    public BalanceServiceTests()
    {
        _balanceService = new BalanceService(_financialAccountRepositoryMock.Object);
    }

    [Fact]
    public async Task GetIncome_ReturnsIncomeTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 100, 100));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetIncome(userId, DefaultCurrency.PLN, _startDate, _endDate);

        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value > 0);
    }

    [Fact]
    public async Task GetSpending_ReturnsSpendingTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, -50, -50));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetSpending(userId, DefaultCurrency.PLN, _startDate, _endDate);

        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value < 0);
    }

    [Fact]
    public async Task GetBalance_ReturnsNetTimeSeries()
    {
        var userId = 1;
        var account = new CurrencyAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 100, 100));
        account.Add(new CurrencyAccountEntry(1, 2, _startDate, -40, -40));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _balanceService.GetBalance(userId, DefaultCurrency.PLN, _startDate, _startDate);

        Assert.NotEmpty(result);
        Assert.Single(result);
        Assert.Equal(60, result.First().Value);
    }
}