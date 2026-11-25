using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

[Trait("Category", "Integration")]
public class TransactionLogPagingTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private readonly Mock<IUserPlanVerifier> _planVerifier = new();
    private TestDatabase? _database;

    // The plan mock is per instance, so the host is too.
    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(_planVerifier.Object);
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null)
            services.Remove(descriptor);
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(100)]
    public async Task Page_WalksOwnerHistoryThroughTypedClient_WithoutGapsOrDuplicates(int pageSize)
    {
        var ct = TestContext.Current.CancellationToken;
        var day = await SeedHistoryAsync(ct);
        var client = new TransactionLogHttpClient(Client);
        var expected = (await client.GetLastTransactions(641, 100)).Where(e => e.Date >= day).ToList();
        Assert.Equal(7, expected.Count);
        var query = new TransactionLogQuery(DateTime.SpecifyKind(day, DateTimeKind.Unspecified), day.AddDays(1), pageSize);
        Assert.Equal(3, expected[0].EntryId);
        Assert.All(expected.Where(e => e.Kind == TransactionKind.Bond), e => Assert.Equal("units", e.CurrencyOrUnit));
        Assert.All(expected.Where(e => e.Kind == TransactionKind.Investment), e => Assert.Equal(("GBX", 2m), (e.CurrencyOrUnit, e.ValueChange)));
        var intraday = await client.GetItemsPage(641, new(day.AddHours(1), day.AddDays(1)), ct);
        Assert.Equal(3, Assert.Single(intraday.Items).EntryId);
        var empty = await client.GetItemsPage(641, new(day.AddYears(1), day.AddYears(2)), ct);
        Assert.Empty(empty.Items);
        Assert.Null(empty.NextCursor);
    }

    // Owner 641 has cash in two currencies, bond units and trades. User 999 has entries of every kind
    // on the same day, which must never show up.
    private async Task<DateTime> SeedHistoryAsync(CancellationToken ct)
    {
        var day = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        var db = _database!.Context;
        db.Currencies.Add(DefaultCurrency.USD);
        db.CurrencyEntries.AddRange(
            new CashEntry(1, day, 1) { UserId = 641, CurrencyId = 0 }, new CashEntry(2, day, 2) { UserId = 641, CurrencyId = 1 },
            new CashEntry(3, day.AddHours(1), 3) { UserId = 641, CurrencyId = 0 },
            new CashEntry(4, day.AddDays(-2), 4) { UserId = 641, CurrencyId = 0 },
            new CashEntry(99, day, 99) { UserId = 999, CurrencyId = 0 });
        db.Set<BondDetails>().Add(new BondDetails("Bond name", "Issuer", DateOnly.FromDateTime(day), DateOnly.FromDateTime(day.AddYears(1)), []) { Id = 7 });
        db.BondEntries.AddRange(new BondEntry(1, day, 1, 7) { UserId = 641 }, new BondEntry(2, day, 2, 7) { UserId = 641 }, new BondEntry(98, day, 98, 7) { UserId = 999 });
        db.AssetListings.Add(new AssetListing
        {
            Id = 1,
            Asset = new Asset { Id = 1, Name = "Asset" },
            Ticker = "HIST",
            ExchangeMic = "XWAR",
            ExchangeName = "Warsaw",
            TradingCurrency = "PLN"
        });
        db.InvestmentTransactions.AddRange(
            new InvestmentTransaction { EntryId = 1, UserId = 641, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "GBX" },
            new InvestmentTransaction { EntryId = 97, UserId = 999, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "PLN" },
            new InvestmentTransaction { EntryId = 2, UserId = 641, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "GBX" });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        Authorize("owner", 641, UserRole.User);
        return day;
    }

    [Theory]
    [InlineData(TransactionKind.Cash, " pln ", 2)]
    [InlineData(TransactionKind.Bond, "PLN", 2)]
    [InlineData(TransactionKind.Investment, "gbx", 2)]
    [InlineData(TransactionKind.Investment, "PLN", 0)]
    [InlineData(TransactionKind.Cash, "XYZ", 0)]
    public async Task KindAndCurrencyFiltersApplyTogether(TransactionKind kind, string code, int expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var day = await SeedHistoryAsync(ct);
        var page = await new TransactionLogHttpClient(Client).GetItemsPage(641, new(day, day.AddDays(1), Kinds: [kind], Currencies: [code]), ct);
        Assert.Equal(expected, page.Items.Count);
        Assert.All(page.Items, row => Assert.Equal(kind, row.Kind));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FilteredPage_WalksEveryPageOfTheSelectionWithoutGapsOrDuplicates(int pageSize)
    {
        var ct = TestContext.Current.CancellationToken;
        var day = await SeedHistoryAsync(ct);
        var client = new TransactionLogHttpClient(Client);
        var query = new TransactionLogQuery(day, day.AddDays(1), pageSize, Kinds: [TransactionKind.Cash, TransactionKind.Bond], Currencies: [" pln "]);

        List<(TransactionKind Kind, long EntryId)> walked = [];
        TransactionLogCursor? cursor = null;
        for (var i = 0; ; i++)
        {
            Assert.True(i < 10, "Cursor did not advance.");
            var page = await client.GetItemsPage(641, query with { Cursor = cursor }, ct);
            Assert.True(page.Items.Count <= pageSize);
            walked.AddRange(page.Items.Select(item => (item.Kind, item.EntryId)));
            if ((cursor = page.NextCursor) is null)
                break;
        }

        // Owner 641's PLN cash entries 3 and 1 and bond entries 1 and 2, newest first with ties broken by id, then kind;
        // USD cash entry 2 and every GBX trade are filtered out.
        Assert.Equal([(TransactionKind.Cash, 3L), (TransactionKind.Bond, 2L), (TransactionKind.Cash, 1L), (TransactionKind.Bond, 1L)], walked);
    }

    // What the Transactions page writes: entries created through the owner routes belong in
    // the log and answer the same kind and currency filters.
    [Fact]
    public async Task EntriesCreatedThroughTheOwnerRoutes_Appear_AndFilterByKindAndCurrency()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = await SeedHistoryAsync(ct);
        var cash = await new CashTransactionsHttpClient(Client).CreateAsync(new CreateCashEntryRequest(1, day.AddHours(5), -4m, "Owner cash", null), ct);
        var bond = await new BondTransactionsHttpClient(Client).CreateAsync(new SaveBondEntryRequest(7, day.AddHours(6), 3m, "Owner bond"), ct);
        var trade = await new InvestmentTradesHttpClient(Client).CreateAsync(
            new CreateInvestmentTradeRequest(1, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN", DateOnly.FromDateTime(day)), ct);
        var client = new TransactionLogHttpClient(Client);
        TransactionLogQuery Query(TransactionKind[]? kinds = null, string[]? currencies = null) =>
            new(day, day.AddDays(1), 100, null, kinds, currencies, null);

        var all = (await client.GetItemsPage(641, Query(), ct)).Items;

        Assert.All([(TransactionKind.Cash, cash.EntryId), (TransactionKind.Bond, bond.EntryId), (TransactionKind.Investment, trade.Id)],
            key => Assert.Single(all, item => (item.Kind, item.EntryId) == key));
        var usdCash = (await client.GetItemsPage(641, Query([TransactionKind.Cash], ["USD"]), ct)).Items;
        Assert.Contains(usdCash, item => item.EntryId == cash.EntryId);
        Assert.All(usdCash, item => Assert.Equal(TransactionKind.Cash, item.Kind));
        var plnBonds = (await client.GetItemsPage(641, Query([TransactionKind.Bond], ["PLN"]), ct)).Items;
        Assert.Contains(plnBonds, item => item.EntryId == bond.EntryId);
        var plnTrades = (await client.GetItemsPage(641, Query([TransactionKind.Investment], ["PLN"]), ct)).Items;
        Assert.Equal([trade.Id], plnTrades.Select(item => item.EntryId));
        var plnCash = (await client.GetItemsPage(641, Query([TransactionKind.Cash], ["PLN"]), ct)).Items;
        Assert.DoesNotContain(plnCash, item => item.EntryId == cash.EntryId);
    }

    [Fact]
    public async Task Currencies_ListsTheOwnersCurrenciesWithoutQuoteUnits()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedHistoryAsync(ct);

        // Owner 641 holds PLN and USD cash, PLN bonds and GBX trades; the GBX quote unit is never offered.
        Assert.Equal(["PLN", "USD"], await new TransactionLogHttpClient(Client).GetCurrencies(641, ct));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("api/TransactionLog/Currencies/999", ct)).StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public async Task ItemsPage_WalksTheSameRowsAsPage_AsTypedEntriesWithTheirLabels(int pageSize)
    {
        var ct = TestContext.Current.CancellationToken;
        var day = await SeedHistoryAsync(ct);
        var db = _database!.Context;
        var label = new FinancialLabel { Name = "Long term" };
        db.FinancialLabels.Add(label);
        (await db.InvestmentTransactions.SingleAsync(t => t.EntryId == 1, ct)).Labels = [label];
        (await db.CurrencyEntries.SingleAsync(e => e.EntryId == 2, ct)).Labels = [label];
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var client = new TransactionLogHttpClient(Client);
        var query = new TransactionLogQuery(day, day.AddDays(1), pageSize);

        var expected = (await client.GetLastTransactions(641, 100)).Where(e => e.Date >= day).Select(e => (e.Kind, e.EntryId)).ToList();
        List<TransactionHistoryItem> actual = [];
        TransactionLogCursor? cursor = null;
        for (var i = 0; ; i++)
        {
            Assert.True(i < 10, "Cursor did not advance.");
            var page = await client.GetItemsPage(641, query with { Cursor = cursor }, ct);
            actual.AddRange(page.Items);
            if ((cursor = page.NextCursor) is null)
                break;
        }

        Assert.Equal(7, expected.Count);
        Assert.Equal(expected, actual.Select(item => (item.Kind, item.EntryId)));
        var savings = Assert.IsType<CurrencyTransactionHistoryItem>(actual.Single(item => item.Kind == TransactionKind.Cash && item.EntryId == 2));
        Assert.Equal(("USD", 1, "Long term"),
            (savings.Currency, savings.Entry.CurrencyId, savings.Entry.Labels.Single().Name));
        Assert.All(actual.OfType<BondTransactionHistoryItem>(), bond => Assert.Equal(7, bond.Entry.BondDetailsId));
        var trade = actual.OfType<InvestmentTransactionHistoryItem>().Single(item => item.EntryId == 1).Transaction;
        Assert.Equal(("HIST", "Long term"), (trade.Ticker, Assert.Single(trade.Labels!).Name));

        // Saving the trade as the edit form does (labels from the row's DTO) keeps its label.
        await new InvestmentTradesHttpClient(Client).UpdateAsync(trade.Id, new UpdateInvestmentTradeRequest(trade.AssetListingId,
            trade.Type, 3m, trade.UnitPrice, trade.Currency, trade.TradeDate, trade.Fee, trade.Notes, [.. trade.Labels!.Select(l => l.Id)]), ct);
        var reloaded = await client.GetItemsPage(641, query with { PageSize = 100, Kinds = [TransactionKind.Investment] }, ct);
        var edited = reloaded.Items.OfType<InvestmentTransactionHistoryItem>().Single(item => item.EntryId == 1).Transaction;
        Assert.Equal((3m, "Long term"), (edited.Quantity, Assert.Single(edited.Labels!).Name));
    }

    public override void Dispose()
    {
        _database?.Dispose();
        base.Dispose();
    }
}