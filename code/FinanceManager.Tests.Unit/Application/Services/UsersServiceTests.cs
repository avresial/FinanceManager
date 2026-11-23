using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class UsersServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock = new();
    private readonly Mock<ILogger<UsersService>> _loggerMock = new();
    private readonly UsersService _usersService;

    public UsersServiceTests() => _usersService = new UsersService(_userRepositoryMock.Object, _cacheInvalidatorMock.Object, _loggerMock.Object);

    [Fact]
    public async Task DeleteUser_RemovesUserAndInvalidatesTheirCache()
    {
        var userId = 1;
        _userRepositoryMock.Setup(repo => repo.RemoveUser(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _usersService.DeleteUser(userId, TestContext.Current.CancellationToken);

        Assert.True(result);
        _userRepositoryMock.Verify(repo => repo.RemoveUser(userId, It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(cache => cache.InvalidateUser(userId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_UserDoesNotExist_ReturnsFalseAndLeavesCacheAlone()
    {
        _userRepositoryMock.Setup(repo => repo.RemoveUser(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _usersService.DeleteUser(1, TestContext.Current.CancellationToken);

        Assert.False(result);
        _cacheInvalidatorMock.Verify(cache => cache.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUser_CacheInvalidationFails_StillReportsTheCommittedDelete()
    {
        _userRepositoryMock.Setup(repo => repo.RemoveUser(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _cacheInvalidatorMock.Setup(cache => cache.InvalidateUser(1, It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("cache down"));

        Assert.True(await _usersService.DeleteUser(1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteUser_RepositoryFailure_Propagates()
    {
        _userRepositoryMock.Setup(repo => repo.RemoveUser(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _usersService.DeleteUser(1, TestContext.Current.CancellationToken));
        _cacheInvalidatorMock.Verify(cache => cache.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}