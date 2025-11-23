using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class UsersServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ILogger<UsersService>> _loggerMock = new();
    private readonly UsersService _usersService;

    public UsersServiceTests() => _usersService = new UsersService(_financialAccountRepositoryMock.Object, _userRepositoryMock.Object, _loggerMock.Object);

    [Fact]
    public async Task DeleteUser_UserExistsWithAccounts_DeletesAccountsAndUser()
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free };
        var accounts = new Dictionary<int, Type> { { 1, typeof(object) }, { 2, typeof(object) } };

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);
        _financialAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId)).ReturnsAsync(accounts);
        _userRepositoryMock.Setup(repo => repo.RemoveUser(userId)).ReturnsAsync(true);

        // Act
        var result = await _usersService.DeleteUser(userId);

        // Assert
        Assert.True(result);
        _financialAccountRepositoryMock.Verify(repo => repo.RemoveAccount(It.IsAny<Type>(), 1), Times.Once);
        _financialAccountRepositoryMock.Verify(repo => repo.RemoveAccount(It.IsAny<Type>(), 2), Times.Once);
        _userRepositoryMock.Verify(repo => repo.RemoveUser(userId), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_UserExistsWithoutAccounts_DeletesUser()
    {
        // Arrange
        var userId = 1;
        var user = new User { UserId = userId, Login = "test", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free };
        var accounts = new Dictionary<int, Type>();

        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync(user);
        _financialAccountRepositoryMock.Setup(repo => repo.GetAvailableAccounts(userId)).ReturnsAsync(accounts);
        _userRepositoryMock.Setup(repo => repo.RemoveUser(userId)).ReturnsAsync(true);

        // Act
        var result = await _usersService.DeleteUser(userId);

        // Assert
        Assert.True(result);
        _financialAccountRepositoryMock.Verify(repo => repo.RemoveAccount(It.IsAny<Type>(), It.IsAny<int>()), Times.Never);
        _userRepositoryMock.Verify(repo => repo.RemoveUser(userId), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_UserDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var userId = 1;
        _userRepositoryMock.Setup(repo => repo.GetUser(userId)).ReturnsAsync((User?)null);

        // Act
        var result = await _usersService.DeleteUser(userId);

        // Assert
        Assert.False(result);
        _financialAccountRepositoryMock.Verify(repo => repo.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        _userRepositoryMock.Verify(repo => repo.RemoveUser(It.IsAny<int>()), Times.Never);
    }
}
