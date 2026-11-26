using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Owner-scoped entry access on a relational provider: the owner predicates, the atomic edit/delete of a linked row with its
/// recalculation, mixed linked and unlinked batches, and the per-owner quota count. Whole amounts keep SQLite's decimal-as-TEXT
/// storage out of the assertions.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteOwnerEntryRepositoryTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private const int _cashAccount = 10;
    private const int _bondAccount = 11;
    private const int _bondA = 5;
    private const int _bondB = 6;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CurrencyEntryRepository _cash;
    private readonly BondEntryRepository _bonds;

    public SqliteOwnerEntryRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.Accounts.AddRange(
            new FinancialAccountBaseDto { AccountId = _cashAccount, UserId = _owner, AccountType = AccountType.Currency, CurrencyId = 0 },
            new FinancialAccountBaseDto { AccountId = _bondAccount, UserId = _owner, AccountType = AccountType.Bond });
        _context.SaveChanges();
        _cash = new CurrencyEntryRepository(_context);
        _bonds = new BondEntryRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static CurrencyAccountEntry Cash(int? account, DateTime date, decimal change, int owner = _owner, int currency = 0) =>
        new(account, 0, date, 0, change) { UserId = owner, CurrencyId = currency, Description = "d" };

    private static BondAccountEntry Bond(int? account, DateTime date, decimal change, int bond = _bondA, int owner = _owner) =>
        new(account, 0, date, 0, change, bond) { UserId = owner, Description = "d" };

    private async Task<List<decimal>> CashValues(int account) =>
        (await _context.CurrencyEntries.AsNoTracking().Where(e => e.AccountId == account).OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).ToListAsync(TestContext.Current.CancellationToken))
        .Select(e => e.Value).ToList();

    [Fact]
    public async Task Cash_MixedBatch_StampsUnlinkedRowsWithoutARunningValue_AndRecalculatesOnlyTheLinkedOnes()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await _cash.Add([Cash(_cashAccount, _jan1, 10), Cash(null, _jan1.AddDays(1), 100), Cash(_cashAccount, _jan1.AddDays(2), 5), Cash(null, _jan1.AddDays(2), -7, _guest)], recalculate: true, ct));

        var rows = await _context.CurrencyEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 0m, 15m, 0m], rows.Select(e => e.Value));
        Assert.Equal([_owner, _owner, _owner, _guest], rows.Select(e => e.UserId!.Value));
        Assert.Equal([_cashAccount, (int?)null, _cashAccount, (int?)null], rows.Select(e => e.AccountId));
        Assert.All(rows, e => Assert.NotEqual(0, e.EntryId));
    }

    [Fact]
    public async Task Cash_UnlinkedRowNeedsAStampedOwnerAndCurrency()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.False(await _cash.Add([Cash(null, _jan1, 1, owner: 0)], recalculate: true, ct));
        Assert.False(await _cash.Add([new CurrencyAccountEntry(null, 0, _jan1, 0, 1) { UserId = _owner }], recalculate: true, ct));
        Assert.Empty(_context.CurrencyEntries);
    }

    [Fact]
    public async Task Cash_GetByOwner_ReturnsOnlyTheOwnersRowsOfOneCurrencyInRange_LinkedOrNot_Ascending()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([
            Cash(_cashAccount, _jan1.AddHours(3), 1),
            Cash(null, _jan1.AddHours(1), 2),
            Cash(null, _jan1.AddHours(2), 3, currency: 1),
            Cash(null, _jan1.AddHours(2), 4, _other),
            Cash(null, _jan1.AddDays(1).AddHours(1), 5),
        ], recalculate: true, ct);

        var rows = await _cash.GetByOwner(_owner, 0, _jan1, _jan1.AddDays(1).AddTicks(-1), ct).ToListAsync(ct);

        Assert.Equal([2m, 1m], rows.Select(e => e.ValueChange));
        Assert.Equal([4m], (await _cash.GetByOwner(_other, 0, _jan1, _jan1.AddDays(5), ct).ToListAsync(ct)).Select(e => e.ValueChange));
        var one = await _cash.GetByOwner(_owner, rows[0].EntryId, ct);
        Assert.Equal(2m, one!.ValueChange);
        Assert.Null(await _cash.GetByOwner(_other, rows[0].EntryId, ct));
    }

    [Fact]
    public async Task Cash_UpdateAndDeleteByOwner_RepairTheLinkedAccount_AndLeaveUnlinkedRowsAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(_cashAccount, _jan1, 10), Cash(_cashAccount, _jan1.AddDays(1), 5), Cash(_cashAccount, _jan1.AddDays(2), 2), Cash(null, _jan1.AddDays(1), 100)], recalculate: true, ct);
        var ids = await _context.CurrencyEntries.AsNoTracking().OrderBy(e => e.EntryId).Select(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 15m, 17m], await CashValues(_cashAccount));

        // The first row moves behind the second: values between the old and the new position change too.
        var edit = new CurrencyAccountEntry(_cashAccount, ids[0], _jan1.AddDays(1).AddHours(12), 0, 20) { Description = "moved" };
        Assert.True(await _cash.UpdateByOwner(_owner, edit, ct));
        Assert.Equal([5m, 25m, 27m], await CashValues(_cashAccount));

        Assert.False(await _cash.UpdateByOwner(_other, edit, ct));
        Assert.False(await _cash.DeleteByOwner(_other, ids[1], ct));
        Assert.True(await _cash.DeleteByOwner(_owner, ids[1], ct));
        Assert.Equal([20m, 22m], await CashValues(_cashAccount));

        // The unlinked row keeps its zero value and is edited or deleted without any account to repair.
        Assert.True(await _cash.UpdateByOwner(_owner, new CurrencyAccountEntry(null, ids[3], _jan1.AddDays(3), 0, 50) { Description = "free" }, ct));
        var free = await _context.CurrencyEntries.AsNoTracking().SingleAsync(e => e.EntryId == ids[3], ct);
        Assert.Equal((50m, 0m, (int?)null, _owner), (free.ValueChange, free.Value, free.AccountId, free.UserId!.Value));
        Assert.True(await _cash.DeleteByOwner(_owner, ids[3], ct));
        Assert.Equal([20m, 22m], await CashValues(_cashAccount));
    }

    [Fact]
    public async Task Cash_UpdateByOwner_NeverChangesTheOwnerOrCurrencyFromTheInput()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(null, _jan1, 1, currency: 1)], recalculate: false, ct);
        var id = (await _context.CurrencyEntries.AsNoTracking().SingleAsync(ct)).EntryId;

        Assert.True(await _cash.UpdateByOwner(_owner, new CurrencyAccountEntry(_cashAccount, id, _jan1, 99, 2) { UserId = _other, CurrencyId = 0, Description = "x" }, ct));

        var stored = await _context.CurrencyEntries.AsNoTracking().SingleAsync(ct);
        Assert.Equal((_owner, 1, (int?)null, 0m, 2m), (stored.UserId!.Value, stored.CurrencyId!.Value, stored.AccountId, stored.Value, stored.ValueChange));
    }

    [Fact]
    public async Task Bonds_MixedBatch_RecalculatesEachInstrumentOfTheLinkedAccountOnly()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await _bonds.Add([
            Bond(_bondAccount, _jan1, 10, _bondA),
            Bond(null, _jan1.AddDays(1), 100, _bondA),
            Bond(_bondAccount, _jan1.AddDays(1), 4, _bondB),
            Bond(_bondAccount, _jan1.AddDays(2), 5, _bondA),
        ], recalculate: true, ct));

        var rows = await _context.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 0m, 4m, 15m], rows.Select(e => e.Value));
    }

    [Fact]
    public async Task Bonds_UpdateByOwner_MovingToAnotherDefinition_RepairsBothPartitions_AndDeleteRepairsTheRest()
    {
        var ct = TestContext.Current.CancellationToken;
        await _bonds.Add([
            Bond(_bondAccount, _jan1, 10, _bondA),
            Bond(_bondAccount, _jan1.AddDays(1), 4, _bondB),
            Bond(_bondAccount, _jan1.AddDays(2), 5, _bondA),
            Bond(null, _jan1.AddDays(1), 100, _bondA),
        ], recalculate: true, ct);
        var ids = await _context.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).Select(e => e.EntryId).ToListAsync(ct);

        var move = new BondAccountEntry(_bondAccount, ids[0], _jan1.AddDays(1).AddHours(12), 0, 20, _bondB) { Description = "moved" };
        Assert.True(await _bonds.UpdateByOwner(_owner, move, ct));

        var values = await _context.BondEntries.AsNoTracking().ToDictionaryAsync(e => e.EntryId, e => e.Value, ct);
        Assert.Equal((24m, 4m, 5m, 0m), (values[ids[0]], values[ids[1]], values[ids[2]], values[ids[3]]));

        Assert.False(await _bonds.DeleteByOwner(_other, ids[1], ct));
        Assert.True(await _bonds.DeleteByOwner(_owner, ids[1], ct));
        values = await _context.BondEntries.AsNoTracking().ToDictionaryAsync(e => e.EntryId, e => e.Value, ct);
        Assert.Equal((20m, 5m, 0m), (values[ids[0]], values[ids[2]], values[ids[3]]));
    }

    [Fact]
    public async Task Bonds_GetByOwner_FiltersByOwnerAndDefinitionInRange()
    {
        var ct = TestContext.Current.CancellationToken;
        await _bonds.Add([
            Bond(_bondAccount, _jan1.AddHours(3), 1),
            Bond(null, _jan1.AddHours(1), 2),
            Bond(null, _jan1.AddHours(2), 3, _bondB),
            Bond(null, _jan1.AddHours(2), 4, owner: _other),
            Bond(null, _jan1.AddDays(5), 5),
        ], recalculate: true, ct);

        var rows = await _bonds.GetByOwner(_owner, _bondA, _jan1, _jan1.AddDays(1).AddTicks(-1), ct).ToListAsync(ct);

        Assert.Equal([2m, 1m], rows.Select(e => e.ValueChange));
        var one = await _bonds.GetByOwner(_owner, rows[0].EntryId, ct);
        Assert.Equal(2m, one!.ValueChange);
        Assert.Null(await _bonds.GetByOwner(_other, rows[0].EntryId, ct));
    }

    [Fact]
    public async Task EntriesCountPerUser_CountsLinkedAndUnlinkedRowsOnceEach_ForTheRequestedOwnersOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cash.Add([Cash(_cashAccount, _jan1, 1), Cash(null, _jan1, 2), Cash(null, _jan1, 3, _guest), Cash(null, _jan1, 4, _other)], recalculate: true, ct);
        await _bonds.Add([Bond(_bondAccount, _jan1, 1), Bond(null, _jan1, 2), Bond(null, _jan1, 3), Bond(null, _jan1, 4, owner: _guest)], recalculate: true, ct);

        var cash = await _cash.GetEntriesCountPerUser([_owner, _guest, 77], ct);
        var bonds = await _bonds.GetEntriesCountPerUser([_owner, _guest, 77], ct);

        Assert.Equal(new Dictionary<int, int> { [_owner] = 2, [_guest] = 1 }, cash.ToDictionary(x => x.Key, x => x.Value));
        Assert.Equal(new Dictionary<int, int> { [_owner] = 3, [_guest] = 1 }, bonds.ToDictionary(x => x.Key, x => x.Value));
        Assert.Empty(await _cash.GetEntriesCountPerUser([], ct));
    }
}