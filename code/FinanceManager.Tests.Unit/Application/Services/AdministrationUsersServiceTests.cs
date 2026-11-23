using FinanceManager.Application.Administration.Users;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AdministrationUsersServiceTests
{
    private readonly Mock<ICashTransactionReader> _cash = new();
    private readonly Mock<IBondTransactionReader> _bondReader = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IActiveUsersRepository> _activeUsersRepositoryMock = new();
    private readonly Mock<IUserPlanVerifier> _userPlanVerifierMock = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICurrencyExchangeService> _exchange = new();
    private readonly Mock<IBondDetailsRepository> _bonds = new();
    private readonly Mock<IInvestmentValuationService> _investments = new();
    private readonly AdministrationUsersService _service;

    public AdministrationUsersServiceTests()
    {
        _activeUsersRepositoryMock.Setup(r => r.GetLastLoginTimes(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, DateTime>());
        _currencies.Setup(x => x.GetByCode("PLN", It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        _userRepositoryMock.Setup(x => x.GetUsersIds(0, int.MaxValue)).Returns(new[] { 1, 2 }.ToAsyncEnumerable());
        _bonds.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(Array.Empty<BondDetails>().ToAsyncEnumerable());
        _cash.Setup(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bondReader.Setup(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _investments.Setup(x => x.GetUserValueByListingAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<long, decimal>());
        _service = new(
            _cash.Object, _bondReader.Object, _userRepositoryMock.Object,
            _activeUsersRepositoryMock.Object, _userPlanVerifierMock.Object,
            _currencies.Object, _exchange.Object, _bonds.Object, _investments.Object);
    }

    [Fact]
    public async Task TotalTrackedMoney_EmptyAccounts_ReturnsZero()
    {
        Assert.Equal(0m, await _service.GetTotalTrackedMoney());
    }

    [Fact]
    public async Task TotalTrackedMoney_ConvertsAllUsersCashAndBonds_AndValuesInvestmentsInPln()
    {
        var now = DateTime.UtcNow;
        // User 1 holds USD cash, user 2 has a negative PLN balance (a debt subtracts) and a USD bond; none of it is linked to an account.
        _cash.Setup(x => x.GetPositions(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(1, 10.25m, now.AddDays(-10))]);
        _cash.Setup(x => x.GetPositions(2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(0, -2m, now.AddDays(-1)), new OwnerLedgerPosition(1, 0m, now.AddDays(-3))]);
        _currencies.Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);
        _currencies.Setup(x => x.GetCurrency(0, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        _exchange.Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>())).ReturnsAsync(4m);
        var details = new BondDetails("USD bond", "issuer", DateOnly.FromDateTime(now.AddYears(-1)), DateOnly.FromDateTime(now.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = now.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }], DefaultCurrency.USD, unitValue: 1m)
        { Id = 5 };
        _bonds.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());
        _bondReader.Setup(x => x.GetPositions(2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(5, 3m, now.AddDays(-10)), new OwnerLedgerPosition(6, 0m, now.AddDays(-20))]);
        // Pooled holdings: valued per user, whether or not the trades are linked to an account.
        _investments.Setup(x => x.GetUserValueByListingAsync(1, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<long, decimal> { [7] = 3000000000.25m });

        // 10.25 USD * 4 - 2 PLN + 3 USD * 4 + investments; the sold-out bond (id 6, no definition) and the zero cash position are skipped.
        Assert.Equal(3000000051.25m, await _service.GetTotalTrackedMoney());
        _exchange.Verify(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TotalTrackedMoney_MissingFx_FailsInsteadOfPublishingPartialTotal()
    {
        _cash.Setup(x => x.GetPositions(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(1, 10m, DateTime.UtcNow.AddDays(-1))]);
        _currencies.Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTotalTrackedMoney());
    }

    [Fact]
    public async Task TotalTrackedMoney_MissingCurrencyOrBondDefinition_Fails()
    {
        _cash.Setup(x => x.GetPositions(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(99, 10m, DateTime.UtcNow.AddDays(-1))]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTotalTrackedMoney());

        _cash.Setup(x => x.GetPositions(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bondReader.Setup(x => x.GetPositions(2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerLedgerPosition(5, 3m, DateTime.UtcNow.AddDays(-10))]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTotalTrackedMoney());
    }

    [Fact]
    public async Task GetUsers_FetchesPageOnce_AndBatchesUsedCapacity()
    {
        // Arrange
        var users = new[]
        {
            new User { UserId = 1, Login = "alice", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Free },
            new User { UserId = 2, Login = "bob", CreationDate = DateTime.UtcNow, PricingLevel = PricingLevel.Premium },
        };

        var aliceLastLogin = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

        _userRepositoryMock.Setup(repo => repo.GetUsers(0, 10)).Returns(users.ToAsyncEnumerable());
        _userPlanVerifierMock.Setup(v => v.GetUsedRecordsCapacity(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 42, [2] = 100 });
        _activeUsersRepositoryMock.Setup(r => r.GetLastLoginTimes(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, DateTime> { [1] = aliceLastLogin });

        // Act
        var result = await _service.GetUsers(0, 10).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(42, result[0].RecordCapacity.UsedCapacity);
        Assert.Equal(PricingProvider.GetMaxAllowedEntries(PricingLevel.Free), result[0].RecordCapacity.TotalCapacity);
        Assert.Equal(100, result[1].RecordCapacity.UsedCapacity);

        // Last-login times are surfaced per user; a user with no login row stays null.
        Assert.Equal(aliceLastLogin, result[0].LastLoggedAt);
        Assert.Null(result[1].LastLoggedAt);

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
        _activeUsersRepositoryMock.Verify(r => r.GetLastLoginTimes(It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
    }
}