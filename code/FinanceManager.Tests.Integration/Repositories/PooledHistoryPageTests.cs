using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class PooledHistoryPageTests
{
    private static readonly DateTime _start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(2);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cash_SkipsOtherOwners_AndKeepsUnknownCurrenciesVisible(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.Currencies.Add(DefaultCurrency.PLN);
        context.CurrencyEntries.AddRange(
            new CashEntry(1, 1, _start, 100m, 1m) { UserId = -1, CurrencyId = 0 },
            new CashEntry(1, 2, _start, 100m, 1m) { UserId = 2, CurrencyId = 0 },
            new CashEntry(1, 4, _start, 100m, 1m) { UserId = -1, CurrencyId = 999 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new CashTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;

        var unfiltered = await repository.GetHistory(-1, new(_start, _end, 10), ct);
        var pln = await repository.GetHistory(-1, new(_start, _end, 10, Currencies: ["PLN"]), ct);

        // The entry whose currency row is missing stays listed, without a currency; it never matches a currency filter.
        Assert.Equal([(4L, (string?)null), (1L, "PLN")], unfiltered.Select(e => (e.EntryId, e.Currency)));
        Assert.Equal([1L], pln.Select(e => e.EntryId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bonds_SkipOtherOwners_AndKeepMissingBondDefinitionsVisible(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.Set<BondDetails>().Add(new BondDetails("Bond name", "Issuer", DateOnly.FromDateTime(_start), DateOnly.FromDateTime(_end), []) { Id = 7 });
        context.BondEntries.AddRange(
            new BondEntry(1, 1, _start, 100m, 1m, 7) { UserId = -1 },
            new BondEntry(1, 2, _start, 100m, 1m, 7) { UserId = 2 },
            new BondEntry(1, 3, _start, 100m, 1m, 999) { UserId = -1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new BondTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;

        var unfiltered = await repository.GetHistory(-1, new(_start, _end, 10), ct);
        var pln = await repository.GetHistory(-1, new(_start, _end, 10, Currencies: [" pln "]), ct);
        var named = await repository.GetHistory(-1, new(_start, _end, 10, Search: "bond NAME"), ct);

        Assert.Equal([(3L, (string?)null, (string?)null), (1L, "Bond name", "PLN")], unfiltered.Select(e => (e.EntryId, e.BondName, e.Currency)));
        Assert.Equal([1L], pln.Select(e => e.EntryId));
        Assert.Equal([1L], named.Select(e => e.EntryId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Entries_WithSeveralLabels_UseOneSlotOfThePage(bool bonds, bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        ILedgerEntry[] entries = [Entry(bonds, 1, 1, _start), Entry(bonds, 1, 2, _start.AddHours(1)), Entry(bonds, 1, 3, _start.AddHours(2))];
        foreach (var entry in entries.Skip(1))
        {
            Labels(entry).Add(new FinancialLabel { Name = $"First {entry.EntryId}" });
            Labels(entry).Add(new FinancialLabel { Name = $"Second {entry.EntryId}" });
        }
        context.AddRange(entries);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var page = await EntryPage(context, bonds, 2);

        Assert.Equal([3L, 2L], page.Select(e => e.EntryId));
        Assert.All(page, e => Assert.Equal(2, e.Labels.Count));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Entries_InclusiveBoundary_KeepsTheEntryOfTheCursor(bool bonds, bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.AddRange(Entry(bonds, 1, 1, _start), Entry(bonds, 1, 2, _start), Entry(bonds, 1, 3, _start));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var ct = TestContext.Current.CancellationToken;

        async Task<long[]> Ids(bool includeEqualId)
        {
            var query = new OwnerHistoryQuery(_start, _end, 10, new(_start, 2, includeEqualId));
            return bonds
                ? [.. (await new BondTransactionRepository(context).GetHistory(-1, query, ct)).Select(e => e.EntryId)]
                : [.. (await new CashTransactionRepository(context).GetHistory(-1, query, ct)).Select(e => e.EntryId)];
        }

        Assert.Equal(new long[] { 2, 1 }, await Ids(true));
        Assert.Equal(new long[] { 1 }, await Ids(false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Investments_HonourIntradayStart_InclusiveBoundary_Currencies_AndEmptyTake(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.AssetListings.Add(new AssetListing
        {
            Id = 1,
            Asset = new Asset { Id = 1, Name = "History asset" },
            Ticker = "HIST",
            ExchangeMic = "XWAR",
            ExchangeName = "Warsaw",
            TradingCurrency = "PLN"
        });
        var day = DateOnly.FromDateTime(_start);
        context.InvestmentTransactions.AddRange(Trade(1, 1, day), Trade(2, 1, day), Trade(3, 1, day));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new InvestmentTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;

        // Trades are stamped at midnight, so a start at noon excludes them and the start of the day keeps them.
        Assert.Empty(await repository.GetHistoryByUser(-1, new(_start, _end, 10), ct));
        Assert.Equal([3L, 2L, 1L], (await repository.GetHistoryByUser(-1, new(_start.Date, _end, 10), ct)).Select(t => t.EntryId));
        Assert.Equal([2L, 1L], (await repository.GetHistoryByUser(-1, new(_start.Date, _end, 10, new(_start.Date, 2, true)), ct)).Select(t => t.EntryId));
        Assert.Equal([1L], (await repository.GetHistoryByUser(-1, new(_start.Date, _end, 10, new(_start.Date, 2, false)), ct)).Select(t => t.EntryId));
        Assert.Equal(3, (await repository.GetHistoryByUser(-1, new(_start.Date, _end, 10, Currencies: [" pln "]), ct)).Count);
        Assert.Empty(await repository.GetHistoryByUser(-1, new(_start.Date, _end, 10, Currencies: ["usd"]), ct));
        Assert.Empty(await repository.GetHistoryByUser(-1, new(_start.Date, _end, 0), ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveTake_ReturnsNothing(int take)
    {
        await using var context = CreateContext(false);
        context.AddRange(Entry(false, 1, 1, _start), Entry(true, 1, 2, _start));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;
        var query = new OwnerHistoryQuery(_start, _end, take);

        Assert.Empty(await new CashTransactionRepository(context).GetHistory(-1, query, ct));
        Assert.Empty(await new BondTransactionRepository(context).GetHistory(-1, query, ct));
        Assert.Empty(await new InvestmentTransactionRepository(context).GetHistoryByUser(-1, query, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Currencies_AreListedForTheOwnerOnly(bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        context.Currencies.Add(DefaultCurrency.USD);
        // The bond definition brings its own default (PLN) currency along.
        context.Set<BondDetails>().Add(new BondDetails("Bond name", "Issuer", DateOnly.FromDateTime(_start), DateOnly.FromDateTime(_end), []) { Id = 7 });
        context.AssetListings.Add(new AssetListing
        {
            Id = 1,
            Asset = new Asset { Id = 1, Name = "History asset" },
            Ticker = "HIST",
            ExchangeMic = "XWAR",
            ExchangeName = "Warsaw",
            TradingCurrency = "PLN"
        });
        context.CurrencyEntries.AddRange(
            new CashEntry(1, 1, _start, 100m, 1m) { UserId = -1, CurrencyId = 1 },
            new CashEntry(1, 2, _start, 100m, 1m) { UserId = -1, CurrencyId = 1 },
            new CashEntry(1, 3, _start, 100m, 1m) { UserId = 2, CurrencyId = 0 });
        context.BondEntries.AddRange(new BondEntry(1, 1, _start, 100m, 1m, 7) { UserId = -1 }, new BondEntry(1, 2, _start, 100m, 1m, 7) { UserId = 2 });
        context.InvestmentTransactions.AddRange(Trade(1, 1, DateOnly.FromDateTime(_start)), Trade(2, 3, DateOnly.FromDateTime(_start)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(["USD"], await new CashTransactionRepository(context).GetCurrencies(-1, ct));
        Assert.Equal(["PLN"], await new BondTransactionRepository(context).GetCurrencies(-1, ct));
        Assert.Equal(["PLN"], await new InvestmentTransactionRepository(context).GetCurrenciesByUser(-1, ct));
        Assert.Empty(await new CashTransactionRepository(context).GetCurrencies(5, ct));
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
        AssertIndex<CashEntry>(context, "UserId", "PostingDate", "EntryId");
        AssertIndex<BondEntry>(context, "UserId", "PostingDate", "EntryId");
        AssertIndex<InvestmentTransaction>(context, "AccountId", "PostingDate", "EntryId");
        AssertIndex<InvestmentTransaction>(context, "UserId", "PostingDate");
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
        BondEntry bond => bond.Labels,
        CashEntry currency => currency.Labels,
        _ => throw new ArgumentException("Unsupported test entry", nameof(entry))
    };

    private static ILedgerEntry Entry(bool bonds, int? accountId, int id, DateTime date) => bonds
        ? new BondEntry(accountId, id, date, 100m, 1m, 1) { UserId = accountId == 3 ? 2 : -1 }
        : new CashEntry(accountId, id, date, 100m, 1m) { UserId = accountId == 3 ? 2 : -1, CurrencyId = 0 };

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