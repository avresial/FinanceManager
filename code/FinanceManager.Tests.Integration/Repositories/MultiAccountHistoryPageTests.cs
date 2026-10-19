using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Entries_PageAcrossAccounts_HonoursRangeAndExclusiveCursor(bool bonds, bool sqlite)
    {
        await using var context = CreateContext(sqlite);
        FinancialEntryBase[] entries =
        [
            Entry(bonds, 1, 1, _start.AddTicks(-1)),
            Entry(bonds, 1, 2, _start),
            Entry(bonds, 2, 3, _start.AddDays(1)),
            Entry(bonds, 1, 4, _end),
            Entry(bonds, 2, 5, _end),
            Entry(bonds, 1, 6, _end.AddTicks(1)),
            Entry(bonds, 3, 7, _end)
        ];
        entries[3].Labels.Add(new FinancialLabel { Name = "History label" });
        context.AddRange(entries);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // A caller requesting pageSize + 1 gets its lookahead row without repository-side trimming.
        var lookahead = await EntryPage(context, bonds, [1, 2, 1], 3);
        Assert.Equal([5, 4, 3], lookahead.Select(e => e.EntryId));
        Assert.Equal("History label", Assert.Single(lookahead[1].Labels).Name);
        var first = await EntryPage(context, bonds, [1, 2], 1);
        var second = await EntryPage(context, bonds, [1, 2], 2, first[^1].PostingDate, first[^1].EntryId);
        var third = await EntryPage(context, bonds, [1, 2], 2, second[^1].PostingDate, second[^1].EntryId);
        var exhausted = await EntryPage(context, bonds, [1, 2], 2, third[^1].PostingDate, third[^1].EntryId);

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
            Trade(4, 1, end), Trade(5, 2, end), Trade(6, 1, end.AddDays(1)), Trade(7, 3, end));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new InvestmentTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;

        var lookahead = await repository.GetHistoryPage([1, 2, 1], start, end, 3, cancellationToken: ct);
        Assert.Equal([5L, 4L, 3L], lookahead.Select(t => t.Id));
        Assert.Equal("History asset", lookahead[0].AssetListing.Asset.Name);
        var first = await repository.GetHistoryPage([1, 2], start, end, 1, cancellationToken: ct);
        var second = await repository.GetHistoryPage([1, 2], start, end, 2, first[^1].TradeDate, first[^1].Id, ct);
        var third = await repository.GetHistoryPage([1, 2], start, end, 2, second[^1].TradeDate, second[^1].Id, ct);
        var exhausted = await repository.GetHistoryPage([1, 2], start, end, 2, third[^1].TradeDate, third[^1].Id, ct);

        Assert.Equal([5L, 4L, 3L, 2L], first.Concat(second).Concat(third).Select(t => t.Id));
        Assert.Empty(exhausted);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entries_EmptyAccountsAndNonpositiveTake_DoNotAccessContext(bool bonds)
    {
        var context = CreateContext(false);
        await context.DisposeAsync();

        Assert.Empty(await EntryPage(context, bonds, [], 2));
        Assert.Empty(await EntryPage(context, bonds, [1], 0));
        Assert.Empty(await EntryPage(context, bonds, [1], -1));
    }

    [Fact]
    public async Task Investments_EmptyAccountsAndNonpositiveTake_DoNotAccessContext()
    {
        var context = CreateContext(false);
        await context.DisposeAsync();
        var repository = new InvestmentTransactionRepository(context);
        var start = DateOnly.FromDateTime(_start);
        var end = DateOnly.FromDateTime(_end);
        var ct = TestContext.Current.CancellationToken;

        Assert.Empty(await repository.GetHistoryPage([], start, end, 2, cancellationToken: ct));
        Assert.Empty(await repository.GetHistoryPage([1], start, end, 0, cancellationToken: ct));
        Assert.Empty(await repository.GetHistoryPage([1], start, end, -1, cancellationToken: ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entries_RejectPartialCursorsAndReversedRange(bool bonds)
    {
        await using var context = CreateContext(false);
        await Assert.ThrowsAsync<ArgumentException>(() => EntryPage(context, bonds, [1], 2, _end));
        await Assert.ThrowsAsync<ArgumentException>(() => EntryPage(context, bonds, [1], 2, cursorId: 5));
        await Assert.ThrowsAsync<ArgumentException>(() => EntryPage(context, bonds, [1], 2, start: _end, end: _start));
    }

    [Fact]
    public async Task Investments_RejectPartialCursorsAndReversedRange()
    {
        await using var context = CreateContext(false);
        var repository = new InvestmentTransactionRepository(context);
        var start = DateOnly.FromDateTime(_start);
        var end = DateOnly.FromDateTime(_end);
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetHistoryPage([1], start, end, 2, end, cancellationToken: ct));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetHistoryPage([1], start, end, 2, cursorId: 5, cancellationToken: ct));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetHistoryPage([1], end, start, 2, cancellationToken: ct));
    }

    [Fact]
    public void Model_HasPagingIndexesForEveryStream()
    {
        using var context = CreateContext(false);
        AssertIndex<CurrencyAccountEntry>(context, "AccountId", "PostingDate", "EntryId");
        AssertIndex<BondAccountEntry>(context, "AccountId", "PostingDate", "EntryId");
        AssertIndex<InvestmentTransaction>(context, "AccountId", "TradeDate", "Id");
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

    private static FinancialEntryBase Entry(bool bonds, int accountId, int id, DateTime date) => bonds
        ? new BondAccountEntry(accountId, id, date, 100m, 1m, 1)
        : new CurrencyAccountEntry(accountId, id, date, 100m, 1m);

    private static InvestmentTransaction Trade(long id, int accountId, DateOnly date) => new()
    {
        Id = id,
        AccountId = accountId,
        UserId = 1,
        AssetListingId = 1,
        TradeDate = date,
        Type = InvestmentTransactionType.Buy,
        Quantity = 1m,
        UnitPrice = 100m,
        Currency = "PLN"
    };

    private static async Task<IReadOnlyList<FinancialEntryBase>> EntryPage(
        AppDbContext context, bool bonds, IReadOnlyCollection<int> accountIds, int take,
        DateTime? cursorDate = null, int? cursorId = null, DateTime? start = null, DateTime? end = null)
    {
        var ct = TestContext.Current.CancellationToken;
        return bonds
            ? await new BondEntryRepository(context).GetHistoryPage(accountIds, start ?? _start, end ?? _end, take, cursorDate, cursorId, ct)
            : await new CurrencyEntryRepository(context).GetHistoryPage(accountIds, start ?? _start, end ?? _end, take, cursorDate, cursorId, ct);
    }
}