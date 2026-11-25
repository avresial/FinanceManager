using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Owner-scoped entry access on a relational provider: the owner predicates, edit/delete by id, batches,
/// and the per-owner quota count. Whole amounts keep SQLite's decimal-as-TEXT
/// storage out of the assertions.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteOwnerEntryRepositoryTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private const int _bondA = 5;
    private const int _bondB = 6;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CashEntryRepository _cash;
    private readonly BondEntryRepository _bonds;

    public SqliteOwnerEntryRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.SaveChanges();
        _cash = new CashEntryRepository(_context);
        _bonds = new BondEntryRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static CashEntry Cash(DateTime date, decimal change, int owner = _owner, int currency = 0) =>
        new(0, date, change) { UserId = owner, CurrencyId = currency, Description = "d" };

    private static BondEntry Bond(DateTime date, decimal change, int bond = _bondA, int owner = _owner) =>
        new(0, date, change, bond) { UserId = owner, Description = "d" };

    private async Task<List<decimal>> CashDeltas() =>
        (await _context.CurrencyEntries.AsNoTracking().Where(e => e.UserId == _owner).OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).ToListAsync(TestContext.Current.CancellationToken))
        .Select(e => e.ValueChange).ToList();

    [Fact]
    public async Task Cash_MixedBatch_UsesEachOwnerAndWritesOnlyDeltas()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await _cash.Add([Cash(_jan1, 10), Cash(_jan1.AddDays(1), 100), Cash(_jan1.AddDays(2), 5), Cash(_jan1.AddDays(2), -7, _guest)], ct));

        var rows = await _context.CurrencyEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 100m, 5m, -7m], rows.Select(e => e.ValueChange));
        Assert.Equal([_owner, _owner, _owner, _guest], rows.Select(e => e.UserId));
        Assert.All(rows, e => Assert.NotEqual(0, e.EntryId));
    }

    [Fact]
    public async Task Cash_RowRejectsZeroOwner()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.False(await _cash.Add([Cash(_jan1, 1, owner: 0)], ct));
        Assert.Empty(_context.CurrencyEntries);
    }

    [Fact]
    public async Task Cash_GetByOwner_ReturnsOnlyTheOwnersRowsOfOneCurrencyInRange_Ascending()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([
            Cash(_jan1.AddHours(3), 1),
            Cash(_jan1.AddHours(1), 2),
            Cash(_jan1.AddHours(2), 3, currency: 1),
            Cash(_jan1.AddHours(2), 4, _other),
            Cash(_jan1.AddDays(1).AddHours(1), 5),
        ], ct);

        var rows = await _cash.GetByOwner(_owner, 0, _jan1, _jan1.AddDays(1).AddTicks(-1), ct).ToListAsync(ct);

        Assert.Equal([2m, 1m], rows.Select(e => e.ValueChange));
        Assert.Equal([4m], (await _cash.GetByOwner(_other, 0, _jan1, _jan1.AddDays(5), ct).ToListAsync(ct)).Select(e => e.ValueChange));
        var one = await _cash.GetByOwner(_owner, rows[0].EntryId, ct);
        Assert.Equal(2m, one!.ValueChange);
        Assert.Null(await _cash.GetByOwner(_other, rows[0].EntryId, ct));
    }

    [Fact]
    public async Task Cash_UpdateAndDeleteByOwner_ChangeOnlyTheRequestedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(_jan1, 10), Cash(_jan1.AddDays(1), 5), Cash(_jan1.AddDays(2), 2), Cash(_jan1.AddDays(1), 100)], ct);
        var ids = await _context.CurrencyEntries.AsNoTracking().OrderBy(e => e.EntryId).Select(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 5m, 100m, 2m], await CashDeltas());

        // The first row moves behind the second.
        var edit = new CashEntry(ids[0], _jan1.AddDays(1).AddHours(12), 20) { Description = "moved" };
        Assert.True(await _cash.UpdateByOwner(_owner, edit, ct));
        Assert.Equal([5m, 100m, 20m, 2m], await CashDeltas());

        Assert.False(await _cash.UpdateByOwner(_other, edit, ct));
        Assert.False(await _cash.DeleteByOwner(_other, ids[1], ct));
        Assert.True(await _cash.DeleteByOwner(_owner, ids[1], ct));
        Assert.Equal([100m, 20m, 2m], await CashDeltas());

        // Any row of the owner is edited or deleted by its id.
        Assert.True(await _cash.UpdateByOwner(_owner, new CashEntry(ids[3], _jan1.AddDays(3), 50) { Description = "free" }, ct));
        var free = await _context.CurrencyEntries.AsNoTracking().SingleAsync(e => e.EntryId == ids[3], ct);
        Assert.Equal((50m, _owner), (free.ValueChange, free.UserId));
        Assert.True(await _cash.DeleteByOwner(_owner, ids[3], ct));
        Assert.Equal([20m, 2m], await CashDeltas());
    }

    [Fact]
    public async Task Cash_UpdateByOwner_NeverChangesTheOwnerOrCurrencyFromTheInput()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(_jan1, 1, currency: 1)], ct);
        var id = (await _context.CurrencyEntries.AsNoTracking().SingleAsync(ct)).EntryId;

        Assert.True(await _cash.UpdateByOwner(_owner, new CashEntry(id, _jan1, 2) { UserId = _other, CurrencyId = 0, Description = "x" }, ct));

        var stored = await _context.CurrencyEntries.AsNoTracking().SingleAsync(ct);
        Assert.Equal((_owner, 1, 2m), (stored.UserId, stored.CurrencyId, stored.ValueChange));
    }

    [Fact]
    public async Task Bonds_MixedBatch_PreservesEachDelta()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await _bonds.Add([
            Bond(_jan1, 10, _bondA),
            Bond(_jan1.AddDays(1), 100, _bondA),
            Bond(_jan1.AddDays(1), 4, _bondB),
            Bond(_jan1.AddDays(2), 5, _bondA),
        ], ct));

        var rows = await _context.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 100m, 4m, 5m], rows.Select(e => e.ValueChange));
    }

    [Fact]
    public async Task Bonds_UpdateByOwner_MovingToAnotherDefinition_ChangesOnlyTheSelectedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        await _bonds.Add([
            Bond(_jan1, 10, _bondA),
            Bond(_jan1.AddDays(1), 4, _bondB),
            Bond(_jan1.AddDays(2), 5, _bondA),
            Bond(_jan1.AddDays(1), 100, _bondA),
        ], ct);
        var ids = await _context.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).Select(e => e.EntryId).ToListAsync(ct);

        var move = new BondEntry(ids[0], _jan1.AddDays(1).AddHours(12), 20, _bondB) { Description = "moved" };
        Assert.True(await _bonds.UpdateByOwner(_owner, move, ct));

        var values = await _context.BondEntries.AsNoTracking().ToDictionaryAsync(e => e.EntryId, e => e.ValueChange, ct);
        Assert.Equal((20m, 4m, 5m, 100m), (values[ids[0]], values[ids[1]], values[ids[2]], values[ids[3]]));

        Assert.False(await _bonds.DeleteByOwner(_other, ids[1], ct));
        Assert.True(await _bonds.DeleteByOwner(_owner, ids[1], ct));
        values = await _context.BondEntries.AsNoTracking().ToDictionaryAsync(e => e.EntryId, e => e.ValueChange, ct);
        Assert.Equal((20m, 5m, 100m), (values[ids[0]], values[ids[2]], values[ids[3]]));
    }

    [Fact]
    public async Task Bonds_GetByOwner_FiltersByOwnerAndDefinitionInRange()
    {
        var ct = TestContext.Current.CancellationToken;
        await _bonds.Add([
            Bond(_jan1.AddHours(3), 1),
            Bond(_jan1.AddHours(1), 2),
            Bond(_jan1.AddHours(2), 3, _bondB),
            Bond(_jan1.AddHours(2), 4, owner: _other),
            Bond(_jan1.AddDays(5), 5),
        ], ct);

        var rows = await _bonds.GetByOwner(_owner, _bondA, _jan1, _jan1.AddDays(1).AddTicks(-1), ct).ToListAsync(ct);

        Assert.Equal([2m, 1m], rows.Select(e => e.ValueChange));
        var one = await _bonds.GetByOwner(_owner, rows[0].EntryId, ct);
        Assert.Equal(2m, one!.ValueChange);
        Assert.Null(await _bonds.GetByOwner(_other, rows[0].EntryId, ct));
    }

    [Fact]
    public async Task EntriesCountPerUser_CountsRowsOnceEach_ForTheRequestedOwnersOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(_jan1, 1), Cash(_jan1, 2), Cash(_jan1, 3, _guest), Cash(_jan1, 4, _other)], ct);
        await _bonds.Add([Bond(_jan1, 1), Bond(_jan1, 2), Bond(_jan1, 3), Bond(_jan1, 4, owner: _guest)], ct);

        var cash = await _cash.GetEntriesCountPerUser([_owner, _guest, 77], ct);
        var bonds = await _bonds.GetEntriesCountPerUser([_owner, _guest, 77], ct);

        Assert.Equal(new Dictionary<int, int> { [_owner] = 2, [_guest] = 1 }, cash.ToDictionary(x => x.Key, x => x.Value));
        Assert.Equal(new Dictionary<int, int> { [_owner] = 3, [_guest] = 1 }, bonds.ToDictionary(x => x.Key, x => x.Value));
        Assert.Empty(await _cash.GetEntriesCountPerUser([], ct));
    }
}