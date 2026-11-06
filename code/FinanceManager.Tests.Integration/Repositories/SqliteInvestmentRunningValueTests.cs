using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Checks that <see cref="InvestmentTransaction.Value"/> (units held after the trade) stays equal to the running sum
/// of signed quantities per account and listing after every add, edit and delete. Runs on SQLite so the repository's
/// real database transaction is exercised, which InMemory cannot do.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqliteInvestmentRunningValueTests : IDisposable
{
    private static readonly DateOnly _day = new(2026, 3, 1);

    private readonly SqliteConnection _connection;
    private readonly FailingSaveInterceptor _interceptor = new();
    private readonly AppDbContext _context;
    private readonly InvestmentTransactionRepository _repository;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // Explicit setup keeps connection opening, context creation, and EnsureCreated in deterministic order.
    public SqliteInvestmentRunningValueTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        _context = CreateContext(_interceptor);
        _context.Database.EnsureCreated();
        _repository = new InvestmentTransactionRepository(_context);
    }

    [Fact]
    public async Task Add_StoresRunningUnitsIncludingBackdatedTradesSellsAndOversells()
    {
        var first = await Add(1, 10, InvestmentTransactionType.Buy, 5, 10);
        Assert.Equal(5m, first.Value);

        var sell = await Add(1, 10, InvestmentTransactionType.Sell, 2, 12);
        Assert.Equal(3m, sell.Value);

        // Backdated buy lands before both existing trades and lifts everything after it.
        var backdated = await Add(1, 10, InvestmentTransactionType.Buy, 4, 5);
        Assert.Equal(4m, backdated.Value);
        Assert.Equal([4m, 9m, 7m], await Values(1, 10));

        // Oversell: negative holdings are stored as they are.
        await Add(1, 10, InvestmentTransactionType.Sell, 20, 14);
        Assert.Equal([4m, 9m, 7m, -13m], await Values(1, 10));
        await AssertAllValuesMatchRunningSum();
    }

    [Fact]
    public async Task Add_SameDayTradesAreOrderedByEntryId()
    {
        var buy = await Add(1, 10, InvestmentTransactionType.Buy, 5, 3);
        var sell = await Add(1, 10, InvestmentTransactionType.Sell, 8, 3);
        var buyAgain = await Add(1, 10, InvestmentTransactionType.Buy, 1, 3);

        Assert.Equal(5m, buy.Value);
        Assert.Equal(-3m, sell.Value);
        Assert.Equal(-2m, buyAgain.Value);
        await AssertAllValuesMatchRunningSum();
    }

    [Fact]
    public async Task Update_QuantityTypeAndDateRecalculateLaterTrades()
    {
        var a = await Add(1, 10, InvestmentTransactionType.Buy, 5, 1);
        var b = await Add(1, 10, InvestmentTransactionType.Buy, 3, 2);
        await Add(1, 10, InvestmentTransactionType.Sell, 1, 3);

        await Update(b.EntryId, t => t.Quantity = 6);
        Assert.Equal([5m, 11m, 10m], await Values(1, 10));

        await Update(a.EntryId, t => t.Type = InvestmentTransactionType.Sell);
        Assert.Equal([-5m, 1m, 0m], await Values(1, 10));

        // Moving the first trade after the others reorders the whole partition.
        await Update(a.EntryId, t => t.TradeDate = _day.AddDays(10));
        Assert.Equal([6m, 5m, 0m], await Values(1, 10));
        await AssertAllValuesMatchRunningSum();
    }

    [Fact]
    public async Task Update_ChangingListingRecalculatesBothListingsAndLeavesOthersUntouched()
    {
        var moved = await Add(1, 10, InvestmentTransactionType.Buy, 2, 2);
        await Add(1, 10, InvestmentTransactionType.Buy, 3, 1);
        await Add(1, 10, InvestmentTransactionType.Buy, 4, 3);
        await Add(1, 20, InvestmentTransactionType.Buy, 10, 1);
        await Add(1, 20, InvestmentTransactionType.Sell, 1, 4);
        await Add(2, 10, InvestmentTransactionType.Buy, 7, 2);
        var otherBefore = await Snapshot(except: [(1, 10), (1, 20)]);

        await Update(moved.EntryId, t => t.AssetListingId = 20);

        Assert.Equal([3m, 7m], await Values(1, 10));
        Assert.Equal([10m, 12m, 11m], await Values(1, 20));
        Assert.Equal(otherBefore, await Snapshot(except: [(1, 10), (1, 20)]));
        await AssertAllValuesMatchRunningSum();
    }

    [Fact]
    public async Task Update_TrackedEntityWithListingAlreadyChanged_RecalculatesOldListing()
    {
        var moved = await Add(1, 10, InvestmentTransactionType.Buy, 5, 1);
        await Add(1, 10, InvestmentTransactionType.Buy, 3, 2);

        var tracked = await _context.InvestmentTransactions.FirstAsync(x => x.EntryId == moved.EntryId, Ct);
        tracked.AssetListingId = 20;
        await _repository.Update(tracked, Ct);

        Assert.Equal([3m], await Values(1, 10));
        Assert.Equal([5m], await Values(1, 20));
    }

    [Fact]
    public async Task Delete_RecalculatesRemainingTradesOfThatListingOnly()
    {
        await Add(1, 10, InvestmentTransactionType.Buy, 5, 1);
        var middle = await Add(1, 10, InvestmentTransactionType.Sell, 2, 2);
        await Add(1, 10, InvestmentTransactionType.Buy, 4, 3);
        await Add(1, 20, InvestmentTransactionType.Buy, 9, 2);
        await Add(2, 10, InvestmentTransactionType.Buy, 9, 2);
        var otherBefore = await Snapshot(except: [(1, 10)]);

        Assert.True(await _repository.Delete(middle.EntryId, Ct));

        Assert.Equal([5m, 9m], await Values(1, 10));
        Assert.Equal(otherBefore, await Snapshot(except: [(1, 10)]));
        await AssertAllValuesMatchRunningSum();
    }

    [Fact]
    public async Task Add_IgnoresValueSuppliedByCaller()
    {
        var added = await _repository.Add(Trade(1, 10, InvestmentTransactionType.Buy, 5, 1, value: 999m), Ct);

        Assert.Equal(5m, added.Value);
        Assert.Equal([5m], await Values(1, 10));
    }

    [Fact]
    public async Task Add_WhenRecalculationSaveFails_RollsBackTradeAndKeepsValues()
    {
        await Add(1, 10, InvestmentTransactionType.Buy, 5, 5);
        await Add(1, 10, InvestmentTransactionType.Sell, 1, 6);
        var before = await Snapshot();

        // The second save of an add is the one that writes the recalculated values.
        _interceptor.FailOnSave = _interceptor.Saves + 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.Add(Trade(1, 10, InvestmentTransactionType.Buy, 3, 1), Ct));

        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public async Task Update_WhenRecalculationSaveFails_RollsBackTheEditAndKeepsValues()
    {
        var first = await Add(1, 10, InvestmentTransactionType.Buy, 5, 1);
        await Add(1, 10, InvestmentTransactionType.Sell, 1, 2);
        var before = await Snapshot();

        _interceptor.FailOnSave = _interceptor.Saves + 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Update(first.EntryId, t => t.Quantity = 50));

        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public async Task Delete_WhenRecalculationSaveFails_RollsBackTheDelete()
    {
        var first = await Add(1, 10, InvestmentTransactionType.Buy, 5, 1);
        await Add(1, 10, InvestmentTransactionType.Sell, 1, 2);
        var before = await Snapshot();

        _interceptor.FailOnSave = _interceptor.Saves + 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.Delete(first.EntryId, Ct));

        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public async Task RandomMutations_KeepEveryValueEqualToRecomputedRunningSum()
    {
        var random = new Random(1084);
        var ids = new List<long>();

        for (var step = 0; step < 150; step++)
        {
            var action = ids.Count < 6 ? 0 : random.Next(4);
            if (action <= 1)
            {
                var added = await Add(random.Next(1, 3), 10 + random.Next(3), RandomType(random), RandomQuantity(random), random.Next(0, 12));
                ids.Add(added.EntryId);
            }
            else if (action == 2)
            {
                var id = ids[random.Next(ids.Count)];
                var accountBefore = (await Read()).Single(t => t.EntryId == id).AccountId;
                await Update(id, t =>
                {
                    t.Type = RandomType(random);
                    t.Quantity = RandomQuantity(random);
                    t.TradeDate = _day.AddDays(random.Next(0, 12));
                    if (random.Next(2) == 0) t.AssetListingId = 10 + random.Next(3);
                    t.AccountId = accountBefore;
                });
            }
            else
            {
                var id = ids[random.Next(ids.Count)];
                Assert.True(await _repository.Delete(id, Ct));
                ids.Remove(id);
            }

            await AssertAllValuesMatchRunningSum();
        }
    }

    private static InvestmentTransactionType RandomType(Random random) =>
        random.Next(3) == 0 ? InvestmentTransactionType.Sell : InvestmentTransactionType.Buy;

    private static decimal RandomQuantity(Random random) => random.Next(1, 4000) / 8m;

    private Task<InvestmentTransaction> Add(int accountId, long listingId, InvestmentTransactionType type, decimal quantity, int dayOffset) =>
        _repository.Add(Trade(accountId, listingId, type, quantity, dayOffset), Ct);

    private static InvestmentTransaction Trade(int accountId, long listingId, InvestmentTransactionType type, decimal quantity, int dayOffset, decimal value = 0m) => new()
    {
        AccountId = accountId,
        UserId = 70,
        AssetListingId = listingId,
        Type = type,
        Quantity = quantity,
        UnitPrice = 10,
        Currency = "USD",
        TradeDate = _day.AddDays(dayOffset),
        Value = value
    };

    private async Task<bool> Update(long id, Action<InvestmentTransaction> change)
    {
        var edited = (await Read()).Single(t => t.EntryId == id);
        change(edited);
        return await _repository.Update(edited, Ct);
    }

    private async Task<List<InvestmentTransaction>> Read()
    {
        // A separate context sees only what was committed, never the repository context's tracked state.
        await using var reader = CreateContext();
        return await reader.InvestmentTransactions.AsNoTracking().OrderBy(t => t.PostingDate).ThenBy(t => t.EntryId).ToListAsync(Ct);
    }

    private async Task<List<decimal>> Values(int accountId, long listingId) =>
        (await Read()).Where(t => t.AccountId == accountId && t.AssetListingId == listingId).Select(t => t.Value).ToList();

    private async Task<List<(long Id, int Account, long Listing, decimal Quantity, decimal Value)>> Snapshot(params (int, long)[] except) =>
        (await Read())
            .Where(t => !except.Contains((t.AccountId, t.AssetListingId)))
            .OrderBy(t => t.EntryId)
            .Select(t => (t.EntryId, t.AccountId, t.AssetListingId, t.Quantity, t.Value))
            .ToList();

    private async Task AssertAllValuesMatchRunningSum()
    {
        var rows = await Read();
        foreach (var partition in rows.GroupBy(t => (t.AccountId, t.AssetListingId)))
        {
            var held = 0m;
            foreach (var trade in partition.OrderBy(t => t.PostingDate).ThenBy(t => t.EntryId))
            {
                held += trade.SignedQuantity;
                Assert.Equal(held, trade.Value);
            }
        }
    }

    private AppDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public int Saves { get; private set; }
        public int FailOnSave { get; set; } = -1;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Saves++;
            if (Saves == FailOnSave) throw new InvalidOperationException("Simulated save failure.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}