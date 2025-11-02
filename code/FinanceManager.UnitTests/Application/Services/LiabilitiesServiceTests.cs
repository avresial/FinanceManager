using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class LiabilitiesServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly LiabilitiesService _liabilitiesService;

    public LiabilitiesServiceTests() => _liabilitiesService = new(_financialAccountRepositoryMock.Object);

    [Fact]
    public async Task IsAnyAccountWithLiabilities_ReturnsTrue_WhenAccountHasNegativeValue()
    {
        // Arrange
        var account = new BankAccount(1, 1, "loan", AccountLabel.Loan);
        account.Add(new BankAccountEntry(1, 1, DateTime.UtcNow, -100, -100));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var result = await _liabilitiesService.IsAnyAccountWithLiabilities(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_YieldsLiabilities_ForNegativeValueAccounts()
    {
        // Arrange
        var account = new BankAccount(1, 1, "loan", AccountLabel.Loan);
        account.Add(new BankAccountEntry(1, 1, DateTime.UtcNow, -200, -200));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var list = await _liabilitiesService.GetEndLiabilitiesPerAccount(1, DateTime.UtcNow, DateTime.UtcNow)
        .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(list);
        Assert.Equal("loan", list[0].Name);
        Assert.Equal(-200m, list[0].Value);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_AggregatesByType_ForNegativeValues()
    {
        // Arrange
        var account1 = new BankAccount(1, 1, "loan1", AccountLabel.Loan);
        account1.Add(new BankAccountEntry(1, 1, DateTime.UtcNow, -100, -100));

        var account2 = new BankAccount(1, 2, "loan2", AccountLabel.Loan);
        account2.Add(new BankAccountEntry(1, 1, DateTime.UtcNow, -50, -50));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        // Act
        var list = await _liabilitiesService.GetEndLiabilitiesPerType(1, DateTime.UtcNow, DateTime.UtcNow)
        .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(list);
        Assert.Equal(AccountLabel.Loan.ToString(), list[0].Name);
        Assert.Equal(-150m, list[0].Value);
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_YieldsTimeSeries_ForNegativeValues()
    {
        // Arrange
        var account = new BankAccount(1, 1, "loan", AccountLabel.Loan);
        account.Add(new BankAccountEntry(1, 1, DateTime.UtcNow, -100, -100));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var list = await _liabilitiesService.GetLiabilitiesTimeSeries(1, DateTime.UtcNow, DateTime.UtcNow)
        .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(list);
        Assert.Contains(list, ts => ts.Value < 0);
    }
}
