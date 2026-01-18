using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class UserPlanVerifierTests
{
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _bankAccountRepositoryMock = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _accountEntryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly UserPlanVerifier _userPlanVerifier;

    public UserPlanVerifierTests() => _userPlanVerifier = new(_bankAccountRepositoryMock.Object, _accountEntryRepositoryMock.Object, _userRepositoryMock.Object);

    [Fact]
    public async Task GetUsedRecordsCapacity_ReturnsTotalEntriesAcrossAccounts()
    {
        // Arrange
        var userId = 1;
        var account1 = new AvailableAccount(1, "Account1");
        var account2 = new AvailableAccount(2, "Account2");

        _bankAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(10);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(2)).ReturnsAsync(5);

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(15, result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_ReturnsTrue_WhenUnderLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _bankAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(5);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreAccounts_ReturnsTrue_WhenUnderLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        _bankAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { new AvailableAccount(1, "Account1") }.ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_ReturnsFalse_WhenOverLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _bankAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(PricingProvider.GetMaxAllowedEntries(user.PricingLevel) + 1);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreAccounts_ReturnsFalse_WhenOverLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var limit = PricingProvider.GetMaxAccountCount(user.PricingLevel);
        var accounts = Enumerable.Range(1, limit).Select(i => new AvailableAccount(i, $"Account{i}")).ToArray();
        _bankAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.False(result);
    }
}