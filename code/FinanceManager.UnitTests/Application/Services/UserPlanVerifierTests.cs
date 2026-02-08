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

[Collection("Application")]
[Trait("Category", "Unit")]
public class UserPlanVerifierTests
{
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _currencyAccountRepositoryMock = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _accountEntryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly UserPlanVerifier _userPlanVerifier;

    public UserPlanVerifierTests() => _userPlanVerifier = new(_currencyAccountRepositoryMock.Object, _accountEntryRepositoryMock.Object, _userRepositoryMock.Object);

    [Fact]
    public async Task GetUsedRecordsCapacity_ReturnsTotalEntriesAcrossAccounts()
    {
        // Arrange
        var userId = 1;
        var account1 = new AvailableAccount(1, "Account1");
        var account2 = new AvailableAccount(2, "Account2");

        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
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
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
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

        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
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
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
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
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_ReturnsFalse_WhenExactlyAtLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(limit);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_ReturnsFalse_WhenAddingMultipleEntriesExceedsLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(limit - 5);

        // Act - trying to add 10 entries when only 5 can fit
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 10);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_ReturnsTrue_WhenExactlyFitsLimit(PricingLevel pricingLevel)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(limit - 10);

        // Act - adding exactly 10 to reach limit
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 10);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanAddMoreEntries_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = 999;
        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanAddMoreAccounts_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = 999;
        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_MultipleAccounts_SumsCorrectly()
    {
        // Arrange
        var userId = 1;
        var account1 = new AvailableAccount(1, "Account1");
        var account2 = new AvailableAccount(2, "Account2");
        var account3 = new AvailableAccount(3, "Account3");

        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account1, account2, account3 }.ToAsyncEnumerable());

        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(250);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(2)).ReturnsAsync(375);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(3)).ReturnsAsync(125);

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(750, result);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_NoAccounts_ReturnsZero()
    {
        // Arrange
        var userId = 1;
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CanAddMoreAccounts_ExactlyAtLimit_ReturnsFalse()
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var limit = PricingProvider.GetMaxAccountCount(user.PricingLevel);
        var accounts = Enumerable.Range(1, limit).Select(i => new AvailableAccount(i, $"Account{i}")).ToArray();
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanAddMoreAccounts_OneUnderLimit_ReturnsTrue()
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var limit = PricingProvider.GetMaxAccountCount(user.PricingLevel);
        var accounts = Enumerable.Range(1, limit - 1).Select(i => new AvailableAccount(i, $"Account{i}")).ToArray();
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _userPlanVerifier.CanAddMoreAccounts(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanAddMoreEntries_ZeroEntriesToAdd_ReturnsTrue()
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(limit);

        // Act - trying to add 0 entries when at limit
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 0);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free, 1000)]
    [InlineData(PricingLevel.Basic, 10000)]
    [InlineData(PricingLevel.Premium, 100000)]
    public async Task CanAddMoreEntries_LargeImport_VerifiesAgainstCorrectLimit(PricingLevel pricingLevel, int expectedLimit)
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var account = new AvailableAccount(1, "Account1");
        _currencyAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId))
        .Returns(new[] { account }.ToAsyncEnumerable());
        _accountEntryRepositoryMock.Setup(repo => repo.GetCount(1)).ReturnsAsync(0);

        // Act - trying to add exactly the limit
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, expectedLimit);

        // Assert
        Assert.True(result);
    }
}