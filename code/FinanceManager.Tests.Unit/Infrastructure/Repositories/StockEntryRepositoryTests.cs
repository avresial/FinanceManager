using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class StockEntryRepositoryTests
{
    private const int _accountId = 1;
    private const int _otherAccountId = 2;
    private const string _apple = "US0378331005";
    private const string _microsoft = "US5949181045";

    private static readonly DateTime _jan10 = new(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _jan15 = new(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _jan20 = new(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static StockAccountEntry Entry(int accountId, DateTime date, string isin, decimal value) =>
        new(accountId, 0, date, value, value, isin, InvestmentType.Stock);

    // -------------------------------------------------------------------------
    // GetNextOlder (per-ISIN, date) — account isolation + per-ISIN boundary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetNextOlder_ReturnsYoungestEntryOlderThanDate_PerIsin()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan10, _apple, 100m));
        await repo.Add(Entry(_accountId, _jan15, _apple, 130m));
        await repo.Add(Entry(_accountId, _jan10, _microsoft, 200m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextOlder(_accountId, _jan20);

        Assert.Equal(2, result.Count);
        Assert.Equal(_jan15, result[_apple].PostingDate);   // youngest apple older than jan20
        Assert.Equal(_jan10, result[_microsoft].PostingDate);
    }

    [Fact]
    public async Task GetNextOlder_ExcludesEntriesFromOtherAccounts()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan10, _apple, 100m));
        await repo.Add(Entry(_otherAccountId, _jan15, _microsoft, 999m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextOlder(_accountId, _jan20);

        Assert.True(result.ContainsKey(_apple));
        Assert.False(result.ContainsKey(_microsoft)); // belongs to another account
        Assert.All(result.Values, e => Assert.Equal(_accountId, e.AccountId));
    }

    [Fact]
    public async Task GetNextOlder_ReturnsEmpty_WhenNoEntriesOlderThanDate()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan20, _apple, 100m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextOlder(_accountId, _jan10);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // GetNextYounger (per-ISIN, date) — the account-isolation bug from #410
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetNextYounger_ReturnsOldestEntryYoungerThanDate_PerIsin()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan15, _apple, 100m));
        await repo.Add(Entry(_accountId, _jan20, _apple, 130m));
        await repo.Add(Entry(_accountId, _jan20, _microsoft, 200m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextYounger(_accountId, _jan10);

        Assert.Equal(2, result.Count);
        Assert.Equal(_jan15, result[_apple].PostingDate);   // oldest apple younger than jan10
        Assert.Equal(_jan20, result[_microsoft].PostingDate);
    }

    [Fact]
    public async Task GetNextYounger_ExcludesEntriesFromOtherAccounts()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan15, _apple, 100m));
        // Another account holds a different ISIN — must not leak into the result.
        await repo.Add(Entry(_otherAccountId, _jan20, _microsoft, 999m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextYounger(_accountId, _jan10);

        Assert.True(result.ContainsKey(_apple));
        Assert.False(result.ContainsKey(_microsoft)); // belongs to another account
        Assert.All(result.Values, e => Assert.Equal(_accountId, e.AccountId));
    }

    [Fact]
    public async Task GetNextYounger_ReturnsEmpty_WhenNoEntriesYoungerThanDate()
    {
        await using var ctx = CreateContext();
        var repo = new StockEntryRepository(ctx);
        await repo.Add(Entry(_accountId, _jan10, _apple, 100m));

        var result = await ((IStockAccountEntryRepository<StockAccountEntry>)repo).GetNextYounger(_accountId, _jan20);

        Assert.Empty(result);
    }
}