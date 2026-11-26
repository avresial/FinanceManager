using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// The owner ledger readers behind net worth, assets, liabilities and balances, on a relational provider: pooled sums per currency /
/// bond definition across account-linked and unlinked rows, the opening-plus-range split, owner isolation and guest ids. Whole amounts
/// keep SQLite's decimal-as-TEXT storage out of the assertions.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteOwnerLedgerReadTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CashTransactionRepository _cash;
    private readonly BondTransactionRepository _bonds;
    private readonly InvestmentTransactionRepository _investments;

    public SqliteOwnerLedgerReadTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _cash = new CashTransactionRepository(_context);
        _bonds = new BondTransactionRepository(_context);
        _investments = new InvestmentTransactionRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task AddCash(int owner, int? currency, DateTime date, decimal change, int? account = null, decimal storedValue = 999)
    {
        _context.CurrencyEntries.Add(new CurrencyAccountEntry(account, 0, date, storedValue, change) { UserId = owner, CurrencyId = currency });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddBond(int owner, int bond, DateTime date, decimal change, int? account = null)
    {
        _context.BondEntries.Add(new BondAccountEntry(account, 0, date, 999, change, bond) { UserId = owner });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cash_GetPositions_PoolsPerCurrencyAcrossLinkedAndUnlinkedRows_FromChangesNotStoredValues()
    {
        await AddCash(_owner, 0, _jan1, 100, account: 10);
        await AddCash(_owner, 0, _jan1.AddDays(1), -30);
        await AddCash(_owner, 2, _jan1.AddDays(2), -5);
        await AddCash(_owner, 0, _jan1.AddDays(9), 1000);

        var positions = await _cash.GetPositions(_owner, _jan1.AddDays(5), TestContext.Current.CancellationToken);

        Assert.Equal([new OwnerLedgerPosition(0, 70, _jan1.AddDays(1)), new OwnerLedgerPosition(2, -5, _jan1.AddDays(2))], positions.OrderBy(p => p.PartitionId));
    }

    [Fact]
    public async Task Cash_GetPositions_IgnoresRowsWithoutACurrency_OtherOwners_AndLaterRows_AndAcceptsGuestIds()
    {
        await AddCash(_owner, null, _jan1, 50);
        await AddCash(_other, 0, _jan1, 70);
        await AddCash(_guest, 0, _jan1, 9);
        await AddCash(_guest, 0, _jan1.AddDays(3), 1);

        Assert.Empty(await _cash.GetPositions(_owner, _jan1.AddDays(1), TestContext.Current.CancellationToken));
        Assert.Equal(9, Assert.Single(await _cash.GetPositions(_guest, _jan1.AddDays(1), TestContext.Current.CancellationToken)).Value);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.GetPositions(0, _jan1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetPositions_NormalizesTheTimestampKind()
    {
        await AddCash(_owner, 0, _jan1.AddHours(10), 5);

        var asUnspecified = DateTime.SpecifyKind(_jan1.AddHours(10), DateTimeKind.Unspecified);
        Assert.Single(await _cash.GetPositions(_owner, asUnspecified, TestContext.Current.CancellationToken));
        Assert.Empty(await _cash.GetPositions(_owner, asUnspecified.AddTicks(-1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetValueRange_SplitsThePooledOpeningBalanceFromTheOrderedRangeEntries()
    {
        await AddCash(_owner, 0, _jan1.AddDays(-5), 10);
        await AddCash(_owner, 0, _jan1.AddDays(-1), 5, account: 10);
        await AddCash(_owner, 2, _jan1.AddDays(-2), 3);
        await AddCash(_owner, 0, _jan1.AddDays(2), 7);
        await AddCash(_owner, 0, _jan1.AddDays(1), 4);
        await AddCash(_owner, 0, _jan1.AddDays(1), -1);
        await AddCash(_owner, 0, _jan1.AddDays(30), 100);

        var range = await _cash.GetValueRange(_owner, _jan1, _jan1.AddDays(5), TestContext.Current.CancellationToken);

        Assert.Equal([new OwnerLedgerPosition(0, 15, _jan1.AddDays(-1)), new OwnerLedgerPosition(2, 3, _jan1.AddDays(-2))], range.Opening.OrderBy(p => p.PartitionId));
        Assert.Equal([4m, -1m, 7m], range.Points.Select(p => p.ValueChange));
        Assert.Equal(range.Points.OrderBy(p => p.PostingDate).ThenBy(p => p.EntryId), range.Points);
    }

    [Fact]
    public async Task Cash_GetValueRange_OrdersEntriesPostedAtTheSameInstantByEntryId()
    {
        foreach (var change in new decimal[] { 1, 2, 3 })
            await AddCash(_owner, 0, _jan1, change);

        var range = await _cash.GetValueRange(_owner, _jan1, _jan1, TestContext.Current.CancellationToken);

        Assert.Equal([1m, 2m, 3m], range.Points.Select(p => p.ValueChange));
        Assert.Empty(range.Opening);
    }

    [Fact]
    public async Task Cash_GetValueRange_ReturnsOnlyOpening_WhenTheRangeHasNoEntries_AndEmptyForAReversedRange()
    {
        await AddCash(_owner, 0, _jan1.AddDays(-5), -40);

        var range = await _cash.GetValueRange(_owner, _jan1, _jan1.AddDays(5), TestContext.Current.CancellationToken);

        Assert.Equal(-40, Assert.Single(range.Opening).Value);
        Assert.Empty(range.Points);
        Assert.True((await _cash.GetValueRange(_owner, _jan1.AddDays(5), _jan1, TestContext.Current.CancellationToken)).IsEmpty);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.GetValueRange(0, _jan1, _jan1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bonds_GetPositions_PoolUnitsPerDefinitionWithTheLatestEntryDate()
    {
        await AddBond(_owner, 5, _jan1, 10, account: 11);
        await AddBond(_owner, 5, _jan1.AddDays(3), 2);
        await AddBond(_owner, 6, _jan1.AddDays(1), 4);
        await AddBond(_owner, 6, _jan1.AddDays(2), -4);
        await AddBond(_other, 5, _jan1, 99);
        await AddBond(_owner, 5, _jan1.AddDays(20), 99);

        var positions = await _bonds.GetPositions(_owner, _jan1.AddDays(10), TestContext.Current.CancellationToken);

        Assert.Equal([new OwnerLedgerPosition(5, 12, _jan1.AddDays(3)), new OwnerLedgerPosition(6, 0, _jan1.AddDays(2))], positions.OrderBy(p => p.PartitionId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _bonds.GetPositions(0, _jan1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bonds_GetValueRange_SplitsOpeningFromRangeEntries_ForGuestsToo()
    {
        await AddBond(_guest, 5, _jan1.AddDays(-4), 6);
        await AddBond(_guest, 5, _jan1.AddDays(1), 2, account: 11);
        await AddBond(_guest, 6, _jan1.AddDays(2), 1);
        await AddBond(_owner, 5, _jan1.AddDays(1), 50);

        var range = await _bonds.GetValueRange(_guest, _jan1, _jan1.AddDays(3), TestContext.Current.CancellationToken);

        Assert.Equal(new OwnerLedgerPosition(5, 6, _jan1.AddDays(-4)), Assert.Single(range.Opening));
        Assert.Equal([(5, 2m), (6, 1m)], range.Points.Select(p => (p.PartitionId, p.ValueChange)));
    }

    [Fact]
    public async Task Investments_GetValuationInputsByUser_PoolsLinkedAndUnlinkedTrades_AndOmitsClosedPreWindowPositions()
    {
        var ct = TestContext.Current.CancellationToken;
        _context.Assets.Add(new Asset { Id = 1, Name = "Equity", Type = AssetType.Stock });
        _context.AssetListings.AddRange(
            new AssetListing { Id = 11, AssetId = 1, Ticker = "AAA", ExchangeMic = "XNAS", ExchangeName = "N", TradingCurrency = "USD", PriceMultiplier = 1m, IsActive = true },
            new AssetListing { Id = 12, AssetId = 1, Ticker = "BBB", ExchangeMic = "XNAS", ExchangeName = "N", TradingCurrency = "USD", PriceMultiplier = 1m, IsActive = true });
        InvestmentTransaction Trade(long user, int? account, long listing, InvestmentTransactionType type, decimal quantity, DateTime date) => new()
        {
            UserId = user,
            AccountId = account,
            AssetListingId = listing,
            Type = type,
            Quantity = quantity,
            UnitPrice = 1m,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(date),
            PostingDate = date,
        };
        _context.InvestmentTransactions.AddRange(
            Trade(_guest, 7, 11, InvestmentTransactionType.Buy, 10, _jan1.AddDays(-9)),
            Trade(_guest, null, 11, InvestmentTransactionType.Sell, 4, _jan1.AddDays(-2)),
            Trade(_guest, 8, 12, InvestmentTransactionType.Buy, 5, _jan1.AddDays(-8)),
            Trade(_guest, null, 12, InvestmentTransactionType.Sell, 5, _jan1.AddDays(-7)),
            Trade(_guest, 7, 11, InvestmentTransactionType.Buy, 3, _jan1.AddDays(1)),
            Trade(_guest, null, 11, InvestmentTransactionType.Buy, 2, _jan1.AddDays(1)),
            Trade(_owner, 9, 11, InvestmentTransactionType.Buy, 100, _jan1.AddDays(1)));
        await _context.SaveChangesAsync(ct);

        var inputs = await _investments.GetValuationInputsByUser(_guest, DateOnly.FromDateTime(_jan1), DateOnly.FromDateTime(_jan1.AddDays(5)), ct);

        var opening = Assert.Single(inputs.OpeningPositions);
        Assert.Equal((11L, 6m), (opening.AssetListingId, opening.Quantity));
        Assert.Equal(5m, Assert.Single(inputs.InWindowTrades).SignedQuantity);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _investments.GetValuationInputsByUser(0, DateOnly.FromDateTime(_jan1), DateOnly.FromDateTime(_jan1), ct));
        Assert.Empty((await _investments.GetValuationInputsByUser(_guest, DateOnly.FromDateTime(_jan1.AddDays(5)), DateOnly.FromDateTime(_jan1), ct)).InWindowTrades);
    }
}