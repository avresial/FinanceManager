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
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared;
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
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(Array.Empty<CurrencyAccount>().ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(Array.Empty<BondAccount>().ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(Array.Empty<InvestmentAccount>().ToAsyncEnumerable());
        _investments.Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<int, decimal>());
        _service = new(
            _financialAccountRepositoryMock.Object, _userRepositoryMock.Object,
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
        var cash = new CurrencyAccount(1, 1, "USD cash", [], nextOlderEntry: new CurrencyAccountEntry(1, 1, now.AddDays(-10), 10.25m, 10.25m), currencyId: 1);
        var loan = new CurrencyAccount(2, 2, "PLN loan", [new CurrencyAccountEntry(2, 1, now.AddDays(-1), -2m, -2m)], currencyId: 0);
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(new[] { cash }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(2, It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(new[] { loan }.ToAsyncEnumerable());
        _currencies.Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);
        _currencies.Setup(x => x.GetCurrency(0, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        _exchange.Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>())).ReturnsAsync(4m);
        var details = new BondDetails("USD bond", "issuer", DateOnly.FromDateTime(now.AddYears(-1)), DateOnly.FromDateTime(now.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = now.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }], DefaultCurrency.USD, unitValue: 1m)
        { Id = 5 };
        _bonds.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());
        var bond = new BondAccount(2, 3, "Bonds", [], nextOlderEntries: new() { [5] = new BondAccountEntry(3, 1, now.AddDays(-10), 3m, 3m, 5) });
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(2, It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(new[] { bond }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(new[] { new InvestmentAccount(1, 4, "Investments") }.ToAsyncEnumerable());
        _investments.Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 4 })), DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<int, decimal> { [4] = 3000000000.25m });

        Assert.Equal(3000000051.25m, await _service.GetTotalTrackedMoney());
        _exchange.Verify(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TotalTrackedMoney_MissingFx_FailsInsteadOfPublishingPartialTotal()
    {
        var cash = new CurrencyAccount(1, 1, "USD", [new CurrencyAccountEntry(1, 1, DateTime.UtcNow.AddDays(-1), 10m, 10m)], currencyId: 1);
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), false)).Returns(new[] { cash }.ToAsyncEnumerable());
        _currencies.Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);
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