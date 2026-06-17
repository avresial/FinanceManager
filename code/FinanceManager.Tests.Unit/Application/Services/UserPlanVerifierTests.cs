using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class UserPlanVerifierTests
{
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _currencyAccountRepositoryMock = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _currencyEntryRepositoryMock = new();
    private readonly Mock<IStockAccountEntryRepository<StockAccountEntry>> _stockEntryRepositoryMock = new();
    private readonly Mock<IBondAccountEntryRepository<BondAccountEntry>> _bondEntryRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly UserPlanVerifier _userPlanVerifier;

    public UserPlanVerifierTests() => _userPlanVerifier = new(
        _currencyAccountRepositoryMock.Object, _currencyEntryRepositoryMock.Object,
        _stockEntryRepositoryMock.Object, _bondEntryRepositoryMock.Object, _userRepositoryMock.Object);

    // Used capacity is the sum of the user's entries across every account type, so a test fixes a count per type.
    private void SetupUsedEntries(int userId, int currency = 0, int stock = 0, int bond = 0)
    {
        _currencyEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [userId] = currency });
        _stockEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [userId] = stock });
        _bondEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [userId] = bond });
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_SumsEntriesAcrossAllAccountTypes()
    {
        // Arrange
        var userId = 1;
        SetupUsedEntries(userId, currency: 250, stock: 375, bond: 125);

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(750, result);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_CountsStockEntries_EvenWithoutCurrencyEntries()
    {
        // Arrange
        var userId = 1;
        SetupUsedEntries(userId, currency: 0, stock: 42, bond: 0);

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_NoEntries_ReturnsZero()
    {
        // Arrange
        var userId = 1;
        SetupUsedEntries(userId);

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_Batch_SumsPerUserAcrossTypes_AndZeroDefaults()
    {
        // Arrange - user 1 has a mix of all types, user 2 only stock, user 3 nothing.
        var userIds = new[] { 1, 2, 3 };
        _currencyEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 100 });
        _stockEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 25, [2] = 50 });
        _bondEntryRepositoryMock.Setup(repo => repo.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 5 });

        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(userIds);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(130, result[1]);
        Assert.Equal(50, result[2]);
        Assert.Equal(0, result[3]);
    }

    [Fact]
    public async Task GetUsedRecordsCapacity_Batch_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = await _userPlanVerifier.GetUsedRecordsCapacity(Array.Empty<int>());

        // Assert
        Assert.Empty(result);
        _currencyEntryRepositoryMock.Verify(repo => repo.GetEntriesCountPerUser(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _stockEntryRepositoryMock.Verify(repo => repo.GetEntriesCountPerUser(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _bondEntryRepositoryMock.Verify(repo => repo.GetEntriesCountPerUser(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        SetupUsedEntries(userId, currency: 5);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task CanAddMoreEntries_CountsStockAndBondEntriesTowardLimit(PricingLevel pricingLevel)
    {
        // Arrange - the user is exactly at the limit purely from stock and bond entries (no currency entries)
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = pricingLevel };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        SetupUsedEntries(userId, stock: limit / 2, bond: limit - limit / 2);

        // Act
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, 1);

        // Assert
        Assert.False(result);
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
        SetupUsedEntries(userId, currency: PricingProvider.GetMaxAllowedEntries(user.PricingLevel) + 1);

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
        SetupUsedEntries(userId, currency: PricingProvider.GetMaxAllowedEntries(user.PricingLevel));

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

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        SetupUsedEntries(userId, currency: limit - 5);

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

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        SetupUsedEntries(userId, currency: limit - 10);

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

        var limit = PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
        SetupUsedEntries(userId, currency: limit);

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
        SetupUsedEntries(userId, currency: 0);

        // Act - trying to add exactly the limit
        var result = await _userPlanVerifier.CanAddMoreEntries(userId, expectedLimit);

        // Assert
        Assert.True(result);
    }
}