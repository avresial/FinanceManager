using FinanceManager.Application.Administration.Users;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AdministrationUsersServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IActiveUsersRepository> _activeUsersRepositoryMock = new();
    private readonly Mock<IUserPlanVerifier> _userPlanVerifierMock = new();
    private readonly AdministrationUsersService _service;

    public AdministrationUsersServiceTests() => _service = new(
        _financialAccountRepositoryMock.Object, _userRepositoryMock.Object,
        _activeUsersRepositoryMock.Object, _userPlanVerifierMock.Object);

    [Fact]
    public async Task GetUsers_FetchesPageOnce_AndBatchesUsedCapacity()
    {
        // Arrange
        var users = new[]
        {
            new User { UserId = 1, Login = "alice", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free },
            new User { UserId = 2, Login = "bob", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Premium },
        };

        _userRepositoryMock.Setup(repo => repo.GetUsers(0, 10)).Returns(users.ToAsyncEnumerable());
        _userPlanVerifierMock.Setup(v => v.GetUsedRecordsCapacity(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 42, [2] = 100 });

        // Act
        var result = await _service.GetUsers(0, 10).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(42, result[0].RecordCapacity.UsedCapacity);
        Assert.Equal(PricingProvider.GetMaxAllowedEntries(PricingLevel.Free), result[0].RecordCapacity.TotalCapacity);
        Assert.Equal(100, result[1].RecordCapacity.UsedCapacity);

        // Capacity is resolved with a single batched call, and there is no per-user GetUser lookup.
        _userPlanVerifierMock.Verify(v => v.GetUsedRecordsCapacity(It.IsAny<IReadOnlyCollection<int>>()), Times.Once);
        _userPlanVerifierMock.Verify(v => v.GetUsedRecordsCapacity(It.IsAny<int>()), Times.Never);
        _userRepositoryMock.Verify(repo => repo.GetUser(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetUsers_MissingCapacityEntry_DefaultsToZero()
    {
        // Arrange
        var users = new[]
        {
            new User { UserId = 5, Login = "carol", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Basic },
        };

        _userRepositoryMock.Setup(repo => repo.GetUsers(0, 10)).Returns(users.ToAsyncEnumerable());
        _userPlanVerifierMock.Setup(v => v.GetUsedRecordsCapacity(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, int>());

        // Act
        var result = await _service.GetUsers(0, 10).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        var single = Assert.Single(result);
        Assert.Equal(0, single.RecordCapacity.UsedCapacity);
    }

    [Fact]
    public async Task GetUsers_NoUsers_DoesNotQueryCapacity()
    {
        // Arrange
        _userRepositoryMock.Setup(repo => repo.GetUsers(0, 10)).Returns(Array.Empty<User>().ToAsyncEnumerable());

        // Act
        var result = await _service.GetUsers(0, 10).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        _userPlanVerifierMock.Verify(v => v.GetUsedRecordsCapacity(It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
    }
}