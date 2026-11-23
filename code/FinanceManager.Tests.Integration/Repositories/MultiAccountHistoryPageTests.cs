using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class MultiAccountHistoryPageTests
{
    private static readonly DateTime _start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(2);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvailableCashAccounts_ProjectTheirOwnCurrency_WithoutDroppingMissingMetadata(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.Currencies.AddRange(DefaultCurrency.PLN, DefaultCurrency.USD);
        context.Accounts.AddRange(
            new FinancialAccountBaseDto { AccountId = 1, UserId = 1, Name = "PLN cash", AccountType = AccountType.Currency },
            new FinancialAccountBaseDto { AccountId = 2, UserId = 1, Name = "USD cash", AccountType = AccountType.Currency, CurrencyId = 1 },
            new FinancialAccountBaseDto { AccountId = 3, UserId = 1, Name = "Unknown", AccountType = AccountType.Currency, CurrencyId = 999 },
            new FinancialAccountBaseDto { AccountId = 4, UserId = 2, Name = "Other owner", AccountType = AccountType.Currency });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var accounts = await new CurrencyAccountRepository(context).GetAvailableAccounts(1).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([(1, "PLN"), (2, "USD"), (3, (string?)null)],
            accounts.OrderBy(a => a.AccountId).Select(a => (a.AccountId, a.Currency)));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Entries_PageAcrossAccounts_HonoursRangeAndExclusiveCursor(bool bonds, bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        ILedgerEntry[] entries =
        [
            Entry(bonds, 1, 1, _start.AddTicks(-1)),
            Entry(bonds, 1, 2, _start),
            Entry(bonds, 2, 3, _start.AddDays(1)),
            Entry(bonds, 1, 4, _end),
            Entry(bonds, null, 5, _end),
            Entry(bonds, 1, 6, _end.AddTicks(1)),
            Entry(bonds, 3, 7, _end),
            Entry(bonds, 3, 8, _end)
        ];
        Labels(entries[3]).Add(new FinancialLabel { Name = "History label" });
        context.AddRange(entries);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // A caller requesting pageSize + 1 gets its lookahead row without repository-side trimming.
        var lookahead = await EntryPage(context, bonds, 3);
        Assert.Equal([5, 4, 3], lookahead.Select(e => e.EntryId));
        Assert.Equal("History label", Assert.Single(lookahead[1].Labels).Name);
        var first = await EntryPage(context, bonds, 1);
        var second = await EntryPage(context, bonds, 2, first[^1].PostingDate, first[^1].EntryId);
        var third = await EntryPage(context, bonds, 2, second[^1].PostingDate, second[^1].EntryId);
        var exhausted = await EntryPage(context, bonds, 2, third[^1].PostingDate, third[^1].EntryId);

        Assert.Equal([5, 4, 3, 2], first.Concat(second).Concat(third).Select(e => e.EntryId));
        Assert.Empty(exhausted);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Investments_PageAcrossAccounts_HonoursRangeAndExclusiveCursor(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        var start = DateOnly.FromDateTime(_start);
        var end = DateOnly.FromDateTime(_end);
        var listing = new AssetListing
        {
            Id = 1,
            Asset = new Asset { Id = 1, Name = "History asset" },
            Ticker = "HIST",
            ExchangeMic = "XWAR",
            ExchangeName = "Warsaw",
            TradingCurrency = "PLN"
        };
        context.AssetListings.Add(listing);
        context.InvestmentTransactions.AddRange(
            Trade(1, 1, start.AddDays(-1)), Trade(2, 1, start), Trade(3, 2, start.AddDays(1)),
            Trade(4, 1, end), Trade(5, null, end), Trade(6, 1, end.AddDays(1)), Trade(7, 3, end), Trade(8, 3, end));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new InvestmentTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;

        var lookahead = await repository.GetHistoryByUser(-1, new(_start.Date, _end.Date, 3), ct);
        Assert.Equal([5L, 4L, 3L], lookahead.Select(t => t.EntryId));
        Assert.Equal("History asset", lookahead[0].AssetListing.Asset.Name);
        var first = await repository.GetHistoryByUser(-1, new(_start.Date, _end.Date, 1), ct);
        var second = await repository.GetHistoryByUser(-1, new(_start.Date, _end.Date, 2, new(first[^1].PostingDate, first[^1].EntryId, false)), ct);
        var third = await repository.GetHistoryByUser(-1, new(_start.Date, _end.Date, 2, new(second[^1].PostingDate, second[^1].EntryId, false)), ct);
        var exhausted = await repository.GetHistoryByUser(-1, new(_start.Date, _end.Date, 2, new(third[^1].PostingDate, third[^1].EntryId, false)), ct);

        Assert.Equal([5L, 4L, 3L, 2L], first.Concat(second).Concat(third).Select(t => t.EntryId));
        Assert.Empty(exhausted);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ZeroOwnerIsRejectedBeforeAccessingContext()
    {
        var context = CreateContext(false);
        await context.DisposeAsync();
        var ct = TestContext.Current.CancellationToken;
        var query = new OwnerHistoryQuery(null, null, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new CashTransactionRepository(context).GetHistory(0, query, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new BondTransactionRepository(context).GetHistory(0, query, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new InvestmentTransactionRepository(context).GetHistoryByUser(0, query, ct));
    }

    [Fact]
    public void Model_HasPagingIndexesForEveryStream()
    {
        using var context = CreateContext(false);
        AssertIndex<CurrencyAccountEntry>(context, "UserId", "PostingDate", "EntryId");
        AssertIndex<BondAccountEntry>(context, "UserId", "PostingDate", "EntryId");
        AssertIndex<InvestmentTransaction>(context, "AccountId", "PostingDate", "EntryId");
    }

    private static void AssertIndex<T>(AppDbContext context, params string[] properties) =>
        Assert.Contains(context.Model.FindEntityType(typeof(T))!.GetIndexes(),
            index => index.Properties.Select(p => p.Name).SequenceEqual(properties));

    private static AppDbContext CreateContext(bool sqlite)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlite)
            options.UseSqlite("Data Source=:memory:;Foreign Keys=False");
        else
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        var context = new AppDbContext(options.Options);
        if (sqlite) context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static ICollection<FinancialLabel> Labels(ILedgerEntry entry) => entry switch
    {
        BondAccountEntry bond => bond.Labels,
        CurrencyAccountEntry currency => currency.Labels,
        _ => throw new ArgumentException("Unsupported test entry", nameof(entry))
    };

    private static ILedgerEntry Entry(bool bonds, int? accountId, int id, DateTime date) => bonds
        ? new BondAccountEntry(accountId, id, date, 100m, 1m, 1) { UserId = accountId == 3 ? 2 : -1 }
        : new CurrencyAccountEntry(accountId, id, date, 100m, 1m) { UserId = accountId == 3 ? 2 : -1, CurrencyId = 0 };

    private static InvestmentTransaction Trade(long id, int? accountId, DateOnly date) => new()
    {
        EntryId = id,
        AccountId = accountId,
        UserId = accountId == 3 ? 2 : -1,
        AssetListingId = 1,
        TradeDate = date,
        Type = InvestmentTransactionType.Buy,
        Quantity = 1m,
        UnitPrice = 100m,
        Currency = "PLN"
    };

    private static async Task<IReadOnlyList<(long EntryId, DateTime PostingDate, IReadOnlyList<FinancialLabel> Labels)>> EntryPage(
        AppDbContext context, bool bonds, int take, DateTime? cursorDate = null, long? cursorId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var query = new OwnerHistoryQuery(_start, _end, take, cursorDate.HasValue ? new(cursorDate.Value, cursorId!.Value, false) : null);
        return bonds
            ? [.. (await new BondTransactionRepository(context).GetHistory(-1, query, ct)).Select(e => (e.EntryId, e.PostingDate, e.Labels))]
            : [.. (await new CashTransactionRepository(context).GetHistory(-1, query, ct)).Select(e => (e.EntryId, e.PostingDate, e.Labels))];
    }
}