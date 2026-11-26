using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Runs the date-based investment queries on a relational provider, where a query that still used the
/// computed TradeDate would fail translation. InMemory cannot detect that.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteInvestmentPostingDateQueryTests : IDisposable
{
    private const int _accountId = 7;
    private const long _userId = 70;
    private const long _listingId = 700;
    private static readonly DateOnly _start = new(2026, 1, 1);
    private static readonly DateOnly _end = new(2026, 12, 31);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly InvestmentTransactionRepository _repository;

    // Explicit setup keeps connection opening, context creation, and EnsureCreated in deterministic order.
    public SqliteInvestmentPostingDateQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _repository = new InvestmentTransactionRepository(_context);
    }

    [Fact]
    public async Task GetByUser_ReturnsOnlyTradesInsideInclusiveRange()
    {
        await Seed();

        var result = await _repository.GetByUser(_userId, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal([_start, _start, _end], result.Select(t => t.TradeDate).Order());
    }

    [Fact]
    public async Task Holdings_CountOnlyTradesUpToAsOfDate()
    {
        await Seed();
        var ct = TestContext.Current.CancellationToken;

        var holdings = await _repository.GetHoldingsAsOf([_accountId], _start, ct);
        var byAccount = await _repository.GetHoldingsByAccountAsOf([_accountId], _start, ct);

        Assert.Equal(3m, holdings[_listingId]);
        Assert.Equal(3m, byAccount[_accountId][_listingId]);
    }

    [Fact]
    public async Task GetLatestByListingAsOf_ReturnsLatestTradeOnOrBeforeDate()
    {
        await Seed();

        var latest = await _repository.GetLatestByListingAsOf(_accountId, _start, TestContext.Current.CancellationToken);

        var transaction = Assert.Single(latest);
        Assert.Equal(_start, transaction.TradeDate);
        Assert.Equal(3, transaction.EntryId);
    }

    [Fact]
    public async Task GetCapitalFlowInputs_ReturnsCalendarDatesUpToDate()
    {
        await Seed();

        var flows = await _repository.GetCapitalFlowInputs([_accountId], _start, TestContext.Current.CancellationToken);

        Assert.Equal([_start.AddDays(-1), _start, _start], flows.Select(f => f.TradeDate).Order());
    }

    [Fact]
    public async Task GetHistoryPage_WalksSameDayTradesWithExclusiveCursor()
    {
        await Seed();
        var ct = TestContext.Current.CancellationToken;

        var first = await _repository.GetHistoryByUser(_userId, new(_start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), _end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), 2), ct);
        var second = await _repository.GetHistoryByUser(_userId, new(_start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), _end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), 2, new(first[^1].PostingDate, first[^1].EntryId, false)), ct);

        Assert.Equal([4L, 3L], first.Select(t => t.EntryId));
        Assert.Equal([2L], second.Select(t => t.EntryId));
    }

    [Fact]
    public async Task GetHoldingsByUserAsOf_PoolsLinkedAndUnlinkedTrades_OmitsZeroUnitsAndOrdersByListing()
    {
        await SeedOwnerPool();

        var holdings = await _repository.GetHoldingsByUserAsOf(_userId, _end, TestContext.Current.CancellationToken);

        // Listing 700 nets 3 (linked buy 2 + unlinked buy 2 - sell 1); 702 is closed out; 703 is negative (oversold); other users and later trades are excluded.
        Assert.Equal([_listingId, 703L], holdings.Select(h => h.AssetListingId));
        Assert.Equal([3m, -1m], holdings.Select(h => h.Units));
        Assert.Empty(await _repository.GetHoldingsByUserAsOf(_userId, _start.AddYears(-5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHistoryPageByUser_PagesAcrossLinkedAndUnlinkedTrades_ForOneOwnerOnly()
    {
        await SeedOwnerPool();
        var ct = TestContext.Current.CancellationToken;

        var first = await _repository.GetHistoryPageByUser(_userId, 2, cancellationToken: ct);
        var last = first.Items[^1];
        var second = await _repository.GetHistoryPageByUser(_userId, 2, last.TradeDate, last.EntryId, cancellationToken: ct);
        var filtered = await _repository.GetHistoryPageByUser(_userId, 10, assetListingId: _listingId, type: InvestmentTransactionType.Buy, cancellationToken: ct);

        Assert.True(first.HasMore);
        Assert.Equal(2, first.Items.Count);
        Assert.DoesNotContain(second.Items, t => first.Items.Any(f => f.EntryId == t.EntryId));
        var all = first.Items.Concat(second.Items).ToList();
        Assert.All(all, t => Assert.Equal(_userId, t.UserId));
        Assert.Contains(all, t => t.AccountId is null);
        Assert.All(filtered.Items, t => Assert.Equal(_listingId, t.AssetListingId));
        Assert.Equal(3, filtered.Items.Count);
    }

    private async Task SeedOwnerPool()
    {
        AssetListing Listing(long id) => new()
        {
            Id = id,
            Asset = new Asset { Id = id + 1000, Name = $"Pool {id}", Type = AssetType.ETF },
            Ticker = $"P{id}",
            ExchangeMic = "XNAS",
            ExchangeName = "Test",
            TradingCurrency = "USD",
            PriceMultiplier = 1
        };
        var main = Listing(_listingId);
        var closed = Listing(702);
        var oversold = Listing(703);
        InvestmentTransaction Row(long id, long user, int? account, AssetListing listing, InvestmentTransactionType type, decimal quantity, DateOnly date) => new()
        {
            EntryId = id,
            UserId = user,
            AccountId = account,
            AssetListing = listing,
            Currency = "USD",
            Quantity = quantity,
            UnitPrice = 10,
            Type = type,
            TradeDate = date
        };
        _context.InvestmentTransactions.AddRange(
            Row(1, _userId, _accountId, main, InvestmentTransactionType.Buy, 2, _start),
            Row(2, _userId, null, main, InvestmentTransactionType.Buy, 2, _start),
            Row(3, _userId, null, main, InvestmentTransactionType.Sell, 1, _start.AddDays(1)),
            Row(4, _userId, _accountId, closed, InvestmentTransactionType.Buy, 4, _start),
            Row(5, _userId, null, closed, InvestmentTransactionType.Sell, 4, _start.AddDays(2)),
            Row(6, _userId, null, oversold, InvestmentTransactionType.Sell, 1, _start.AddDays(3)),
            Row(7, _userId + 1, null, main, InvestmentTransactionType.Buy, 50, _start),
            Row(8, _userId, null, main, InvestmentTransactionType.Buy, 9, _end.AddDays(1)));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
    }

    private async Task Seed()
    {
        var listing = new AssetListing
        {
            Id = _listingId,
            Asset = new Asset { Id = 701, Name = "Date test", Type = AssetType.ETF },
            Ticker = "DATE",
            ExchangeMic = "XNAS",
            ExchangeName = "Test",
            TradingCurrency = "USD",
            PriceMultiplier = 1
        };
        // IDs 1 and 5 fall just outside the range; 2 and 3 share a day to exercise the ID tiebreak.
        _context.InvestmentTransactions.AddRange(
            Trade(1, _start.AddDays(-1), listing),
            Trade(2, _start, listing),
            Trade(3, _start, listing),
            Trade(4, _end, listing),
            Trade(5, _end.AddDays(1), listing));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
    }

    private static InvestmentTransaction Trade(long id, DateOnly date, AssetListing listing) => new()
    {
        EntryId = id,
        AccountId = _accountId,
        UserId = _userId,
        AssetListing = listing,
        Currency = "USD",
        Quantity = 1,
        UnitPrice = 10,
        Type = InvestmentTransactionType.Buy,
        TradeDate = date
    };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}