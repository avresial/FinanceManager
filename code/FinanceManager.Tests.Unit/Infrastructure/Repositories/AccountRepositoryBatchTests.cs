using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Exercises the batched <see cref="AccountRepository.GetAccounts{T}"/> path added for issue #411,
/// asserting that loading a whole user's accounts produces the same per-account shape (in-range entries,
/// next-older / next-younger boundaries, empty-range fallback) as the original per-account loop.
/// </summary>
[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class AccountRepositoryBatchTests
{
    private const int _userId = 1;

    private static readonly DateTime _start = new(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AccountRepository CreateRepository(AppDbContext ctx) =>
        new(new CurrencyAccountRepository(ctx), new StockAccountRepository(ctx), new BondAccountRepository(ctx),
            new StockEntryRepository(ctx), new CurrencyEntryRepository(ctx), new BondEntryRepository(ctx));

    private static void AddAccount(AppDbContext ctx, int accountId, AccountType type, AccountLabel label = AccountLabel.Other) =>
        ctx.Accounts.Add(new FinancialAccountBaseDto { AccountId = accountId, UserId = _userId, Name = $"Account {accountId}", AccountType = type, AccountLabel = label });

    // -------------------------------------------------------------------------
    // Currency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAccounts_Currency_LoadsEachAccountsOwnEntries_OrderedNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Currency, AccountLabel.Cash);
        AddAccount(ctx, 2, AccountType.Currency, AccountLabel.Loan);
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 101, new DateTime(2025, 1, 12, 0, 0, 0, DateTimeKind.Utc), 100, 100));
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 102, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), 250, 150));
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(2, 201, new DateTime(2025, 1, 14, 0, 0, 0, DateTimeKind.Utc), 50, 50));
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var accounts = await repo.GetAccounts<CurrencyAccount>(_userId, _start, _end).ToListAsync(ct);

        Assert.Equal(2, accounts.Count);

        var first = accounts.Single(a => a.AccountId == 1);
        Assert.Equal(AccountLabel.Cash, first.AccountType);
        Assert.Equal([102, 101], first.Entries!.Select(e => e.EntryId));

        var second = accounts.Single(a => a.AccountId == 2);
        Assert.Equal([201], second.Entries!.Select(e => e.EntryId));
    }

    [Fact]
    public async Task GetAccounts_Currency_SetsNextOlderAndNextYoungerBoundaries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Currency);
        // Next-older is taken before _start and next-younger after _end, so the in-range row (100) must NOT be
        // mistaken for the younger boundary — that boundary is the first entry after the window (110).
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 90, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc), 10, 10));    // older than _start
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 100, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), 25, 15));  // inside [_start, _end]
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 110, new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc), 45, 20));  // younger than _end
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var account = (await repo.GetAccounts<CurrencyAccount>(_userId, _start, _end).ToListAsync(ct)).Single();

        Assert.Equal(100, account.Entries!.Single().EntryId);
        Assert.NotNull(account.NextOlderEntry);
        Assert.Equal(90, account.NextOlderEntry!.EntryId);
        Assert.NotNull(account.NextYoungerEntry);
        Assert.Equal(110, account.NextYoungerEntry!.EntryId);
    }

    [Fact]
    public async Task GetAccounts_Currency_NextOlder_BreaksTiesByEntryId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Currency);
        var sameDay = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 90, sameDay, 10, 10));
        ctx.CurrencyEntries.Add(new CurrencyAccountEntry(1, 95, sameDay, 20, 10));
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var account = (await repo.GetAccounts<CurrencyAccount>(_userId, _start, _end).ToListAsync(ct)).Single();

        // No in-range entries -> falls back to the next-older entry, which must be the higher EntryId on the tied date.
        Assert.Equal(95, account.NextOlderEntry!.EntryId);
        Assert.Equal(95, account.Entries!.Single().EntryId);
    }

    [Fact]
    public async Task GetAccounts_Currency_ReturnsEmpty_ForUserWithNoAccounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var repo = CreateRepository(ctx);

        Assert.Empty(await repo.GetAccounts<CurrencyAccount>(_userId, _start, _end).ToListAsync(ct));
    }

    // -------------------------------------------------------------------------
    // Stock — per-instrument (ISIN) boundaries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAccounts_Stock_ResolvesNextOlderPerIsin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Stock);
        var older = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.StockEntries.Add(new StockAccountEntry(1, 10, older, 5, 5, "AAA", InvestmentType.Stock));
        ctx.StockEntries.Add(new StockAccountEntry(1, 11, older, 7, 7, "BBB", InvestmentType.Stock));
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var account = (await repo.GetAccounts<StockAccount>(_userId, _start, _end).ToListAsync(ct)).Single();

        Assert.Equal(2, account.NextOlderEntries.Count);
        Assert.Equal(10, account.NextOlderEntries["AAA"].EntryId);
        Assert.Equal(11, account.NextOlderEntries["BBB"].EntryId);
        // No in-range entries -> entries fall back to the per-ISIN older boundary values.
        Assert.Equal(2, account.Entries!.Count);
    }

    [Fact]
    public async Task GetAccounts_Stock_KeepsInRangeEntries_PerAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Stock);
        AddAccount(ctx, 2, AccountType.Stock);
        ctx.StockEntries.Add(new StockAccountEntry(1, 10, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), 5, 5, "AAA", InvestmentType.Stock));
        ctx.StockEntries.Add(new StockAccountEntry(2, 20, new DateTime(2025, 1, 16, 0, 0, 0, DateTimeKind.Utc), 9, 9, "BBB", InvestmentType.Stock));
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var accounts = await repo.GetAccounts<StockAccount>(_userId, _start, _end).ToListAsync(ct);

        Assert.Equal(10, accounts.Single(a => a.AccountId == 1).Entries!.Single().EntryId);
        Assert.Equal(20, accounts.Single(a => a.AccountId == 2).Entries!.Single().EntryId);
    }

    // -------------------------------------------------------------------------
    // Bond — per-instrument (bond details id) boundaries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAccounts_Bond_ResolvesNextOlderPerBond()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        AddAccount(ctx, 1, AccountType.Bond);
        var older = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        ctx.BondEntries.Add(new BondAccountEntry(1, 10, older, 5, 5, 100));
        ctx.BondEntries.Add(new BondAccountEntry(1, 11, older, 8, 8, 200));
        ctx.BondEntries.Add(new BondAccountEntry(1, 12, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), 12, 4, 100));
        await ctx.SaveChangesAsync(ct);

        var repo = CreateRepository(ctx);
        var account = (await repo.GetAccounts<BondAccount>(_userId, _start, _end).ToListAsync(ct)).Single();

        Assert.Equal(AccountLabel.Bond, account.AccountType);
        Assert.Equal(12, account.Entries!.Single().EntryId);
        Assert.Equal(2, account.NextOlderEntries.Count);
        Assert.Equal(10, account.NextOlderEntries[100].EntryId);
        Assert.Equal(11, account.NextOlderEntries[200].EntryId);
    }
}