using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>Verifies that entry repositories handle identifiers that do not fit in 32 bits.</summary>
[Trait("Category", "Integration")]
public sealed class SqliteLongEntryIdTests : IDisposable
{
    private const long _beyondIntId = 5_000_000_000;
    private static readonly DateTime _postingDate = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    // Explicit setup keeps connection opening, context creation, and EnsureCreated in deterministic order.
    public SqliteLongEntryIdTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CurrencyEntry_BeyondIntId_CanBeReadPagedAndDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        _context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, _beyondIntId, _postingDate, 100, 1),
            new CurrencyAccountEntry(1, _beyondIntId + 1, _postingDate, 101, 1));
        await _context.SaveChangesAsync(ct);
        _context.ChangeTracker.Clear();
        var repository = new CurrencyEntryRepository(_context);

        var entry = await repository.Get(1, _beyondIntId);
        var page = await repository.GetHistoryPage([1], _postingDate, _postingDate, 10, _postingDate, _beyondIntId + 1, ct);
        var deleted = await repository.Delete(1, _beyondIntId, ct);

        Assert.Equal(_beyondIntId, entry?.EntryId);
        Assert.Equal(_beyondIntId, Assert.Single(page).EntryId);
        Assert.True(deleted);
    }

    [Fact]
    public async Task BondEntry_BeyondIntId_CanBeReadAndDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        _context.BondEntries.Add(new BondAccountEntry(1, _beyondIntId, _postingDate, 100, 1, 1));
        await _context.SaveChangesAsync(ct);
        _context.ChangeTracker.Clear();
        var repository = new BondEntryRepository(_context);

        var entry = await repository.Get(1, _beyondIntId);
        var deleted = await repository.Delete(1, _beyondIntId, ct);

        Assert.Equal(_beyondIntId, entry?.EntryId);
        Assert.True(deleted);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}