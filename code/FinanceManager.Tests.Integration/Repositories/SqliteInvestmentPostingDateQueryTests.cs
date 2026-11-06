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

        var first = await _repository.GetHistoryPage([_accountId], _start, _end, 2, cancellationToken: ct);
        var second = await _repository.GetHistoryPage([_accountId], _start, _end, 2, first[^1].TradeDate, first[^1].EntryId, ct);

        Assert.Equal([4L, 3L], first.Select(t => t.EntryId));
        Assert.Equal([2L], second.Select(t => t.EntryId));
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