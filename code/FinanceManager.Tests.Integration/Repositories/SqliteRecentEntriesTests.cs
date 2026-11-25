using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Verifies that recent currency and bond reads stay at one bounded relational command as the number
/// of accounts grows. The dashboard account stream supplies the ownership boundary before it invokes
/// these repository methods; the repository query itself is deliberately limited to those ids.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteRecentEntriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CommandCounter _commands = new();
    private readonly AppDbContext _context;

    // Explicit setup keeps connection opening, context creation, and EnsureCreated in deterministic order.
    public SqliteRecentEntriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CurrencyRecentRead_FiltersAndOrdersAcrossAccounts()
    {
        _context.CurrencyEntries.AddRange(
            new CashEntry(10, new DateTime(2024, 3, 1), 1) { UserId = 1, CurrencyId = 1 },
            new CashEntry(11, new DateTime(2024, 3, 3), 1) { UserId = 1, CurrencyId = 1 },
            new CashEntry(12, new DateTime(2024, 3, 2), 1) { UserId = 1, CurrencyId = 1 },
            new CashEntry(13, new DateTime(2024, 3, 4), 1) { UserId = 2, CurrencyId = 1 });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var result = await new CashTransactionRepository(_context)
            .GetHistory(1, new(null, null, 2, IncludeDetails: false), TestContext.Current.CancellationToken);

        Assert.Equal([11, 12], result.Select(entry => entry.EntryId));
        Assert.All(result, entry => Assert.Empty(entry.Labels));
        Assert.Equal(1, _commands.Count);
    }

    [Fact]
    public async Task BondRecentRead_FiltersAndOrdersAcrossAccounts()
    {
        _context.BondEntries.AddRange(
            new BondEntry(10, new DateTime(2024, 3, 1), 1, 7) { UserId = 1 },
            new BondEntry(11, new DateTime(2024, 3, 3), 1, 7) { UserId = 1 },
            new BondEntry(12, new DateTime(2024, 3, 2), 1, 7) { UserId = 1 },
            new BondEntry(13, new DateTime(2024, 3, 4), 1, 7) { UserId = 2 });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var result = await new BondTransactionRepository(_context)
            .GetHistory(1, new(null, null, 2, IncludeDetails: false), TestContext.Current.CancellationToken);

        Assert.Equal([11, 12], result.Select(entry => entry.EntryId));
        Assert.All(result, entry => Assert.Empty(entry.Labels));
        Assert.Equal(1, _commands.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task CurrencyRecentRead_UsesOneBoundedQuery(int accountCount)
    {
        var accountIds = Enumerable.Range(1, accountCount).ToArray();
        foreach (var accountId in accountIds)
        {
            _context.CurrencyEntries.Add(new CashEntry(0,
                new DateTime(2024, 1, accountId % 28 + 1, 0, 0, 0, DateTimeKind.Utc),
                accountId)
            { UserId = 1, CurrencyId = 1 });
            _context.CurrencyEntries.Add(new CashEntry(0,
                new DateTime(2024, 2, accountId % 28 + 1, 0, 0, 0, DateTimeKind.Utc),
                accountId)
            { UserId = 1, CurrencyId = 1 });
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var repository = new CashTransactionRepository(_context);
        var result = await repository
            .GetHistory(1, new(null, null, 3, IncludeDetails: false), TestContext.Current.CancellationToken);
        var coldQueryCount = _commands.Count;
        _commands.Reset();
        var warmResult = await repository
            .GetHistory(1, new(null, null, 3, IncludeDetails: false), TestContext.Current.CancellationToken);

        Assert.Equal(Math.Min(3, accountCount * 2), result.Count);
        Assert.Equal(result.Select(entry => entry.EntryId), warmResult.Select(entry => entry.EntryId));
        Assert.Equal(1, coldQueryCount);
        Assert.Equal(1, _commands.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task BondRecentRead_UsesOneBoundedQuery(int accountCount)
    {
        var accountIds = Enumerable.Range(1, accountCount).ToArray();
        foreach (var accountId in accountIds)
        {
            _context.BondEntries.Add(new BondEntry(0,
                new DateTime(2024, 1, accountId % 28 + 1, 0, 0, 0, DateTimeKind.Utc),
                accountId,
                1)
            { UserId = 1 });
            _context.BondEntries.Add(new BondEntry(0,
                new DateTime(2024, 2, accountId % 28 + 1, 0, 0, 0, DateTimeKind.Utc),
                accountId,
                1)
            { UserId = 1 });
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var repository = new BondTransactionRepository(_context);
        var result = await repository
            .GetHistory(1, new(null, null, 3, IncludeDetails: false), TestContext.Current.CancellationToken);
        var coldQueryCount = _commands.Count;
        _commands.Reset();
        var warmResult = await repository
            .GetHistory(1, new(null, null, 3, IncludeDetails: false), TestContext.Current.CancellationToken);

        Assert.Equal(Math.Min(3, accountCount * 2), result.Count);
        Assert.Equal(result.Select(entry => entry.EntryId), warmResult.Select(entry => entry.EntryId));
        Assert.Equal(1, coldQueryCount);
        Assert.Equal(1, _commands.Count);
    }

    [Fact]
    public async Task CashRecentRead_RemainsBoundedForOwnersWithThousandsOfAccounts()
    {
        const int accountCount = 2_001;
        var accountIds = Enumerable.Range(1, accountCount).ToArray();
        foreach (var accountId in accountIds)
        {
            _context.CurrencyEntries.Add(new CashEntry(0,
                new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                accountId)
            { UserId = 1, CurrencyId = 1 });
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var result = await new CashTransactionRepository(_context)
            .GetHistory(1, new(null, null, 2, IncludeDetails: false), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, _commands.Count);
    }

    [Fact]
    public async Task BondNameRead_UsesOneQueryAndOmitsMissingDetails()
    {
        var bond = new BondDetails(
            "EDO0134",
            "Treasury",
            new DateOnly(2024, 1, 1),
            new DateOnly(2034, 12, 31),
            [],
            currency: DefaultCurrency.PLN);
        _context.Bonds.Add(bond);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var result = await new BondDetailsRepository(_context)
            .GetNamesByIdsAsync([bond.Id, bond.Id + 1000], TestContext.Current.CancellationToken);

        Assert.Equal("EDO0134", result[bond.Id]);
        Assert.DoesNotContain(bond.Id + 1000, result.Keys);
        Assert.Equal(1, _commands.Count);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }
}