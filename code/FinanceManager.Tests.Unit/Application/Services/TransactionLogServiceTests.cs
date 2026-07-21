using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class TransactionLogServiceTests
{
    private const int _userId = 1;

    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _currencyAccountRepository = new();
    private readonly Mock<IAccountRepository<BondAccount>> _bondAccountRepository = new();
    private readonly Mock<IAccountRepository<InvestmentAccount>> _investmentAccountRepository = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _currencyEntryRepository = new();
    private readonly Mock<IAccountEntryRepository<BondAccountEntry>> _bondEntryRepository = new();
    private readonly Mock<IInvestmentTransactionRepository> _investmentTransactionRepository = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepository = new();
    private readonly TransactionLogService _service;

    public TransactionLogServiceTests()
    {
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId)).Returns(ToAsyncEnumerable<AvailableAccount>([]));
        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId)).Returns(ToAsyncEnumerable<AvailableAccount>([]));
        _investmentAccountRepository.Setup(r => r.GetAvailableAccounts(_userId)).Returns(ToAsyncEnumerable<AvailableAccount>([]));

        _service = new TransactionLogService(
            _currencyAccountRepository.Object,
            _bondAccountRepository.Object,
            _investmentAccountRepository.Object,
            _currencyEntryRepository.Object,
            _bondEntryRepository.Object,
            _investmentTransactionRepository.Object,
            _bondDetailsRepository.Object);
    }

    [Fact]
    public async Task GetLastTransactions_MergesAllAccountTypes_NewestFirst()
    {
        // Arrange
        SetupCurrencyAccount(accountId: 10, "Bank", [
            new CurrencyAccountEntry(10, 1, new DateTime(2024, 3, 5), 100, -50) { Description = "Groceries" },
            new CurrencyAccountEntry(10, 2, new DateTime(2024, 3, 1), 150, 150) { Description = "Salary" },
        ]);

        SetupBondAccount(accountId: 20, "Bonds", [new BondAccountEntry(20, 3, new DateTime(2024, 3, 3), 10, 10, bondDetailsId: 7)]);
        _bondDetailsRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BondDetails("EDO0134", "Treasury", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), []));

        SetupInvestmentAccount(accountId: 30, "Broker", [new InvestmentTransaction
        {
            Id = 4,
            AccountId = 30,
            Type = InvestmentTransactionType.Buy,
            Quantity = 2,
            UnitPrice = 500,
            TradeDate = new DateOnly(2024, 3, 4),
            AssetListing = new AssetListing { Ticker = "CSPX" },
        }]);

        // Act
        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal([new DateTime(2024, 3, 5), new DateTime(2024, 3, 4), new DateTime(2024, 3, 3), new DateTime(2024, 3, 1)],
            result.Select(x => x.Date));

        var currencyEntry = result[0];
        Assert.Equal(AccountType.Currency, currencyEntry.AccountType);
        Assert.Equal("Bank", currencyEntry.AccountName);
        Assert.Equal("Groceries", currencyEntry.Description);
        Assert.Equal(-50, currencyEntry.ValueChange);

        var investmentEntry = result[1];
        Assert.Equal(AccountType.Stock, investmentEntry.AccountType);
        Assert.Equal("Broker", investmentEntry.AccountName);
        Assert.Equal("Buy 2 CSPX", investmentEntry.Description);
        Assert.Equal(1000, investmentEntry.ValueChange);

        var bondEntry = result[2];
        Assert.Equal(AccountType.Bond, bondEntry.AccountType);
        Assert.Equal("EDO0134", bondEntry.Description);
    }

    [Fact]
    public async Task GetLastTransactions_LimitsMergedResultToCount()
    {
        // Arrange
        SetupCurrencyAccount(accountId: 10, "Bank", [
            new CurrencyAccountEntry(10, 1, new DateTime(2024, 3, 5), 100, -50) { Description = "Newest" },
            new CurrencyAccountEntry(10, 2, new DateTime(2024, 3, 4), 150, 10) { Description = "Middle" },
            new CurrencyAccountEntry(10, 3, new DateTime(2024, 3, 3), 140, 5) { Description = "Oldest" },
        ]);

        // Act
        var result = await _service.GetLastTransactions(_userId, 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(["Newest", "Middle"], result.Select(x => x.Description));
    }

    [Fact]
    public async Task GetLastTransactions_ReturnsEmpty_WhenCountIsNotPositive()
    {
        // Act
        var result = await _service.GetLastTransactions(_userId, 0, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        _currencyAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetLastTransactions_ReturnsEmpty_WhenUserHasNoAccounts()
    {
        // Act
        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLastTransactions_FallsBackToContractorDetails_WhenDescriptionIsEmpty()
    {
        // Arrange
        SetupCurrencyAccount(accountId: 10, "Bank", [
            new CurrencyAccountEntry(10, 1, new DateTime(2024, 3, 5), 100, -50) { Description = "", ContractorDetails = "Shop" },
        ]);

        // Act
        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Shop", Assert.Single(result).Description);
    }

    [Fact]
    public async Task GetLastTransactions_DoesNotQueryEntries_WhileAccountStreamIsStillOpen()
    {
        // A scoped EF Core DbContext allows only one active query: issuing an entry
        // query while the accounts stream is still enumerating throws on relational
        // providers ("A second operation was started on this context instance...").
        var currencyStreamOpen = new System.Runtime.CompilerServices.StrongBox<bool>(false);
        var bondStreamOpen = new System.Runtime.CompilerServices.StrongBox<bool>(false);

        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(TrackedAsyncEnumerable([new AvailableAccount(10, "Bank")], currencyStreamOpen));
        _currencyEntryRepository.Setup(r => r.Get(10, It.IsAny<DateTime>(), It.IsAny<int>(), true))
            .Callback(() => Assert.False(currencyStreamOpen.Value, "Entry query issued while the currency accounts stream was still open."))
            .ReturnsAsync([]);

        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(TrackedAsyncEnumerable([new AvailableAccount(20, "Bonds")], bondStreamOpen));
        _bondEntryRepository.Setup(r => r.Get(20, It.IsAny<DateTime>(), It.IsAny<int>(), true))
            .Callback(() => Assert.False(bondStreamOpen.Value, "Entry query issued while the bond accounts stream was still open."))
            .ReturnsAsync([]);

        // Act
        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        _currencyEntryRepository.Verify(r => r.Get(10, It.IsAny<DateTime>(), It.IsAny<int>(), true), Times.Once);
        _bondEntryRepository.Verify(r => r.Get(20, It.IsAny<DateTime>(), It.IsAny<int>(), true), Times.Once);
    }

    private void SetupCurrencyAccount(int accountId, string name, List<CurrencyAccountEntry> entries)
    {
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _currencyEntryRepository.Setup(r => r.Get(accountId, It.IsAny<DateTime>(), It.IsAny<int>(), true))
            .ReturnsAsync(entries);
    }

    private void SetupBondAccount(int accountId, string name, List<BondAccountEntry> entries)
    {
        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _bondEntryRepository.Setup(r => r.Get(accountId, It.IsAny<DateTime>(), It.IsAny<int>(), true))
            .ReturnsAsync(entries);
    }

    private void SetupInvestmentAccount(int accountId, string name, List<InvestmentTransaction> transactions)
    {
        _investmentAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _investmentTransactionRepository.Setup(r => r.GetMostRecentByAccounts(It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(accountId)), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }

    // Mirrors a deferred EF query: isOpen stays true from the first MoveNextAsync
    // until the stream is exhausted, like an open data reader.
    private static async IAsyncEnumerable<T> TrackedAsyncEnumerable<T>(IEnumerable<T> items, System.Runtime.CompilerServices.StrongBox<bool> isOpen)
    {
        isOpen.Value = true;
        try
        {
            foreach (var item in items)
                yield return item;
            await Task.CompletedTask;
        }
        finally
        {
            isOpen.Value = false;
        }
    }
}