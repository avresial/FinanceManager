using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
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
        _bondDetailsRepository.Setup(r => r.GetNamesByIdsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(7)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [7] = "EDO0134" });

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
    public async Task GetLastTransactions_BatchesBondNameResolution_AndFallsBackForMissingDetails()
    {
        SetupBondAccount(accountId: 20, "Bonds", [
            new BondAccountEntry(20, 2, new DateTime(2024, 3, 2), 10, 10, bondDetailsId: 8),
            new BondAccountEntry(20, 1, new DateTime(2024, 3, 1), 10, 10, bondDetailsId: 7),
        ]);
        _bondDetailsRepository.Setup(r => r.GetNamesByIdsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(7) && ids.Contains(8)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [7] = "EDO0134" });

        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        Assert.Equal(["Bond", "EDO0134"], result.Select(entry => entry.Description));
        _bondDetailsRepository.Verify(r => r.GetNamesByIdsAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(7) && ids.Contains(8)),
            It.IsAny<CancellationToken>()), Times.Once);
        _bondDetailsRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLastTransactions_PassesOnlyAvailableAccountIdsToBatchQueries()
    {
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([
                new AvailableAccount(10, "Bank"),
                new AvailableAccount(11, "Savings"),
            ]));
        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(20, "Bonds")]));
        _currencyEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11)),
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bondEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(20)),
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.GetLastTransactions(_userId, 3, TestContext.Current.CancellationToken);

        _currencyEntryRepository.Verify(r => r.GetMostRecentByAccounts(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11)),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
        _bondEntryRepository.Verify(r => r.GetMostRecentByAccounts(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(20)),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
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
        _currencyEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(10)),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.False(currencyStreamOpen.Value, "Entry query issued while the currency accounts stream was still open."))
            .ReturnsAsync([]);

        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(TrackedAsyncEnumerable([new AvailableAccount(20, "Bonds")], bondStreamOpen));
        _bondEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(20)),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.False(bondStreamOpen.Value, "Entry query issued while the bond accounts stream was still open."))
            .ReturnsAsync([]);

        // Act
        var result = await _service.GetLastTransactions(_userId, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        _currencyEntryRepository.Verify(r => r.GetMostRecentByAccounts(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(10)),
            10,
            It.IsAny<CancellationToken>()), Times.Once);
        _bondEntryRepository.Verify(r => r.GetMostRecentByAccounts(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(20)),
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    public async Task GetPage_WalksEveryEntryExactlyOnce_InDashboardOrder(int pageSize)
    {
        var day = new DateTime(2024, 3, 5);
        SetupCurrencyAccount(10, "Bank", [
            new(10, 1, day, 10, 1), new(10, 2, day, 10, 2),
            new(10, int.MaxValue, day, 10, 3), new(10, 4, day.AddHours(1), 10, 4),
            new(10, 5, day.AddDays(-1), 10, 5), new(10, 6, day.AddDays(-2), 10, 6)]);
        SetupBondAccount(20, "Bonds", [
            new(20, 1, day, 10, 1, 7), new(20, 2, day, 10, 2, 7),
            new(20, int.MaxValue, day, 10, 3, 7)]);
        _bondDetailsRepository.Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [7] = "Bond name" });
        SetupInvestmentAccount(30, "Broker", [
            new() { Id = 1, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2 },
            new() { Id = 2, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2 },
            new() { Id = int.MaxValue, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2 },
            new() { Id = long.MaxValue, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2 }]);

        var expected = (await _service.GetLastTransactions(_userId, 100, TestContext.Current.CancellationToken))
            .Where(e => e.Date >= day.AddDays(-1)).ToList();
        var query = new TransactionLogQuery(day.AddDays(-1), day.AddDays(1), pageSize);
        var first = await _service.GetPage(_userId, query, TestContext.Current.CancellationToken);
        Assert.Equal(expected.Take(pageSize), first.Items);
        Assert.Equal([AccountType.Currency, AccountType.Bond, AccountType.Stock],
            expected.Where(e => e.Date == day && e.EntryId == 1).Select(e => e.AccountType));

        List<TransactionLogEntryDto> actual = [];
        var page = first;
        for (var i = 0; ; i++)
        {
            Assert.True(i < 20, "Cursor failed to advance.");
            actual.AddRange(page.Items);
            if (page.NextCursor is not TransactionLogCursor next)
                break;
            var last = page.Items[^1];
            Assert.Equal(new TransactionLogCursor(last.Date, last.AccountType, last.EntryId), next);
            page = await _service.GetPage(_userId, query with { Cursor = next }, TestContext.Current.CancellationToken);
        }
        Assert.Equal(expected, actual);
        Assert.Equal(actual.Count, actual.Select(e => (e.AccountType, e.EntryId)).Distinct().Count());
    }

    [Fact]
    public async Task GetPage_IntradayRange_ExcludesMidnightTrades_AndIncludesExactEndpoints()
    {
        var day = new DateTime(2024, 3, 5);
        SetupCurrencyAccount(10, "Bank", [new(10, 1, day, 0, 1), new(10, 2, day.AddHours(1), 0, 2)]);
        SetupInvestmentAccount(30, "Broker", [
            new() { Id = 1, AccountId = 30, TradeDate = DateOnly.FromDateTime(day) },
            new() { Id = 2, AccountId = 30, TradeDate = DateOnly.FromDateTime(day.AddDays(1)) }]);

        var result = await _service.GetPage(_userId, new(day.AddHours(1), day.AddDays(1)), TestContext.Current.CancellationToken);

        Assert.Equal([AccountType.Stock, AccountType.Currency], result.Items.Select(e => e.AccountType));
        Assert.All(result.Items, e => Assert.Equal(2, e.EntryId));
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task GetPage_NoAccounts_ReturnsEmptyWithoutEntryQueries()
    {
        var page = await _service.GetPage(_userId, new(DateTime.MinValue, DateTime.MaxValue), TestContext.Current.CancellationToken);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
        _currencyEntryRepository.VerifyNoOtherCalls();
        _bondEntryRepository.VerifyNoOtherCalls();
        _investmentTransactionRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPage_EmptyStreams_UsesOnlyOwnerAccounts_AndBoundedSequentialQueries()
    {
        var start = new DateTime(2024, 1, 1);
        var end = start.AddDays(1);
        var streamOpen = new System.Runtime.CompilerServices.StrongBox<bool>(false);
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(TrackedAsyncEnumerable([new AvailableAccount(10, "Bank"), new AvailableAccount(11, "Savings")], streamOpen));
        var pending = new TaskCompletionSource<IReadOnlyList<CurrencyAccountEntry>>();
        _currencyEntryRepository.Setup(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11)),
            start, end, 3, null, null, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.False(streamOpen.Value)).Returns(pending.Task);
        SetupBondAccount(20, "Bonds", []);
        SetupInvestmentAccount(30, "Broker", []);

        var task = _service.GetPage(_userId, new(start, end, 2), TestContext.Current.CancellationToken);
        Assert.False(task.IsCompleted);
        _bondAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        pending.SetResult([]);
        var result = await task;

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
        _currencyEntryRepository.Verify(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11)),
            start, end, 3, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _bondEntryRepository.Verify(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(20)),
            start, end, 3, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _investmentTransactionRepository.Verify(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(30)),
            DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), 3, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPage_AccountTypeFilter_SkipsUnselectedStreamsEntirely()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountTypes: [AccountType.Bond]),
            TestContext.Current.CancellationToken);

        Assert.All(page.Items, e => Assert.Equal(AccountType.Bond, e.AccountType));
        Assert.Equal(2, page.Items.Count);
        _currencyAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        _investmentAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        VerifyCurrencyEntryQueries(Times.Never());
        VerifyInvestmentEntryQueries(Times.Never());
        VerifyBondEntryQueries(Times.Once());
    }

    [Fact]
    public async Task GetPage_MultipleAccountTypes_QueriesOnlyTheSelectedStreams()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountTypes: [AccountType.Stock, AccountType.Currency]),
            TestContext.Current.CancellationToken);

        Assert.Equal([AccountType.Currency, AccountType.Currency, AccountType.Currency, AccountType.Currency, AccountType.Stock, AccountType.Stock],
            page.Items.Select(e => e.AccountType).Order());
        _bondAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        VerifyBondEntryQueries(Times.Never());
    }

    [Fact]
    public async Task GetPage_EmptyFilters_BehaveLikeNoFilter()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountTypes: [], AccountIds: []),
            TestContext.Current.CancellationToken);

        Assert.Equal(8, page.Items.Count);
        Assert.Equal(3, page.Items.Select(e => e.AccountType).Distinct().Count());
    }

    [Fact]
    public async Task GetPage_AccountIdFilter_RestrictsToThoseAccounts()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountIds: [11]),
            TestContext.Current.CancellationToken);

        Assert.All(page.Items, e => Assert.Equal(11, e.AccountId));
        Assert.Equal(2, page.Items.Count);
        _currencyEntryRepository.Verify(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(11)),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        // None of the other accounts is selected, so their streams never reach an entry query.
        VerifyBondEntryQueries(Times.Never());
        VerifyInvestmentEntryQueries(Times.Never());
    }

    [Fact]
    public async Task GetPage_AccountIdFilter_IgnoresIdsOfOtherUsers()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountIds: [10, 999]),
            TestContext.Current.CancellationToken);

        Assert.All(page.Items, e => Assert.Equal(10, e.AccountId));
        Assert.Equal(2, page.Items.Count);
        _currencyEntryRepository.Verify(r => r.GetHistoryPage(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(10)),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPage_OnlyForeignAccountIds_ReturnsEmptyPageWithoutEntryQueries()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        var page = await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), AccountIds: [999, 1000]),
            TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
        VerifyCurrencyEntryQueries(Times.Never());
        VerifyBondEntryQueries(Times.Never());
        VerifyInvestmentEntryQueries(Times.Never());
        _bondDetailsRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPage_TypeAndAccountIdFilters_AreCombinedWithAnd()
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);

        // Account 30 is an investment account, which the type filter excludes; account 20 is a bond account
        // that the id filter excludes.
        var page = await _service.GetPage(_userId,
            new(day.AddDays(-1), day.AddDays(1), AccountTypes: [AccountType.Currency, AccountType.Bond], AccountIds: [10, 30]),
            TestContext.Current.CancellationToken);

        Assert.All(page.Items, e => Assert.Equal((10, AccountType.Currency), (e.AccountId, e.AccountType)));
        Assert.Equal(2, page.Items.Count);
        _investmentAccountRepository.Verify(r => r.GetAvailableAccounts(It.IsAny<int>()), Times.Never);
        VerifyBondEntryQueries(Times.Never());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetPage_WithFilters_PagesWithoutGapsOrDuplicates(int pageSize)
    {
        var day = new DateTime(2024, 3, 5);
        SetupAllStreams(day);
        var unfiltered = (await _service.GetPage(_userId, new(day.AddDays(-1), day.AddDays(1), 100), TestContext.Current.CancellationToken)).Items;
        var expected = unfiltered.Where(e => e.AccountType != AccountType.Bond && e.AccountId != 11).ToList();
        Assert.Equal(4, expected.Count);
        var query = new TransactionLogQuery(day.AddDays(-1), day.AddDays(1), pageSize,
            AccountTypes: [AccountType.Currency, AccountType.Stock], AccountIds: [10, 30, 20, 999]);

        List<TransactionLogEntryDto> actual = [];
        var page = await _service.GetPage(_userId, query, TestContext.Current.CancellationToken);
        for (var i = 0; ; i++)
        {
            Assert.True(i < 20, "Cursor failed to advance.");
            actual.AddRange(page.Items);
            if (page.NextCursor is not TransactionLogCursor next)
                break;
            page = await _service.GetPage(_userId, query with { Cursor = next }, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, actual);
    }

    // Two currency accounts (10, 11), one bond account (20) and one investment account (30), each with
    // entries on the same two days so every filter has something to include and something to exclude.
    private void SetupAllStreams(DateTime day)
    {
        SetupCurrencyAccount(10, "Bank", [
            new(10, 1, day, 10, 1), new(10, 2, day.AddDays(-1), 10, 2),
            new(11, 3, day, 10, 3), new(11, 4, day.AddDays(-1), 10, 4)]);
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(() => ToAsyncEnumerable([new AvailableAccount(10, "Bank"), new AvailableAccount(11, "Savings")]));
        SetupBondAccount(20, "Bonds", [new(20, 1, day, 10, 1, 7), new(20, 2, day.AddDays(-1), 10, 2, 7)]);
        _bondDetailsRepository.Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [7] = "Bond name" });
        SetupInvestmentAccount(30, "Broker", [
            new() { Id = 1, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2 },
            new() { Id = 2, AccountId = 30, TradeDate = DateOnly.FromDateTime(day.AddDays(-1)), Quantity = 1, UnitPrice = 2 }]);
    }

    private void VerifyCurrencyEntryQueries(Times times) =>
        _currencyEntryRepository.Verify(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), times);

    private void VerifyBondEntryQueries(Times times) =>
        _bondEntryRepository.Verify(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), times);

    private void VerifyInvestmentEntryQueries(Times times) =>
        _investmentTransactionRepository.Verify(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), times);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetPage_RejectsInvalidPageSizes(int size)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetPage(_userId,
            new(DateTime.MinValue, DateTime.MaxValue, size), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPage_RejectsReversedRangeAndInvalidCursor()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetPage(_userId,
            new(DateTime.MaxValue, DateTime.MinValue), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetPage(_userId,
            new(DateTime.MinValue, DateTime.MaxValue, Cursor: new(DateTime.MinValue, (AccountType)99, 1)), TestContext.Current.CancellationToken));
    }

    private void SetupCurrencyAccount(int accountId, string name, List<CurrencyAccountEntry> entries)
    {
        _currencyAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _currencyEntryRepository.Setup(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, DateTime start, DateTime end, int take, DateTime? date, int? id, CancellationToken _) =>
                entries.Where(e => ids.Contains(e.AccountId) && e.PostingDate >= start && e.PostingDate <= end &&
                    (date == null || e.PostingDate < date || (e.PostingDate == date && e.EntryId < id)))
                .OrderByDescending(e => e.PostingDate).ThenByDescending(e => e.EntryId).Take(take).ToList());
        _currencyEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(accountId)),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    private void SetupBondAccount(int accountId, string name, List<BondAccountEntry> entries)
    {
        _bondAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _bondEntryRepository.Setup(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, DateTime start, DateTime end, int take, DateTime? date, int? id, CancellationToken _) =>
                entries.Where(e => ids.Contains(e.AccountId) && e.PostingDate >= start && e.PostingDate <= end &&
                    (date == null || e.PostingDate < date || (e.PostingDate == date && e.EntryId < id)))
                .OrderByDescending(e => e.PostingDate).ThenByDescending(e => e.EntryId).Take(take).ToList());
        _bondEntryRepository.Setup(r => r.GetMostRecentByAccounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(accountId)),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    private void SetupInvestmentAccount(int accountId, string name, List<InvestmentTransaction> transactions)
    {
        _investmentAccountRepository.Setup(r => r.GetAvailableAccounts(_userId))
            .Returns(ToAsyncEnumerable([new AvailableAccount(accountId, name)]));
        _investmentTransactionRepository.Setup(r => r.GetHistoryPage(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, DateOnly start, DateOnly end, int take, DateOnly? date, long? id, CancellationToken _) =>
                transactions.Where(e => ids.Contains(e.AccountId) && e.TradeDate >= start && e.TradeDate <= end &&
                    (date == null || e.TradeDate < date || (e.TradeDate == date && e.Id < id)))
                .OrderByDescending(e => e.TradeDate).ThenByDescending(e => e.Id).Take(take).ToList());
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