using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

[Trait("Category", "Integration")]
public class TransactionLogPagingTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private TestDatabase? _database;

    protected override void ConfigureServices(IServiceCollection services)
    {
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
        var day = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        var db = _database!.Context;
        db.Accounts.AddRange(
            new FinancialAccountBaseDto { AccountId = 10, UserId = 641, Name = "Bank", AccountType = AccountType.Currency },
            new FinancialAccountBaseDto { AccountId = 11, UserId = 641, Name = "Savings", AccountType = AccountType.Currency },
            new FinancialAccountBaseDto { AccountId = 20, UserId = 641, Name = "Bonds", AccountType = AccountType.Bond },
            new FinancialAccountBaseDto { AccountId = 30, UserId = 641, Name = "Broker", AccountType = AccountType.Stock },
            new FinancialAccountBaseDto { AccountId = 98, UserId = 999, Name = "Other bonds", AccountType = AccountType.Bond },
            new FinancialAccountBaseDto { AccountId = 97, UserId = 999, Name = "Other broker", AccountType = AccountType.Stock },
            new FinancialAccountBaseDto { AccountId = 99, UserId = 999, Name = "Other user", AccountType = AccountType.Currency });
        db.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(10, 1, day, 0, 1), new CurrencyAccountEntry(11, 2, day, 0, 2),
            new CurrencyAccountEntry(10, 3, day.AddHours(1), 0, 3),
            new CurrencyAccountEntry(10, 4, day.AddDays(-2), 0, 4),
            new CurrencyAccountEntry(99, 99, day, 0, 99));
        db.Set<BondDetails>().Add(new BondDetails("Bond name", "Issuer", DateOnly.FromDateTime(day), DateOnly.FromDateTime(day.AddYears(1)), []) { Id = 7 });
        db.BondEntries.AddRange(new BondAccountEntry(20, 1, day, 0, 1, 7), new BondAccountEntry(20, 2, day, 0, 2, 7), new BondAccountEntry(98, 98, day, 0, 98, 7));
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
            new InvestmentTransaction { Id = 1, AccountId = 30, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "PLN" },
            new InvestmentTransaction { Id = 97, AccountId = 97, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "PLN" },
            new InvestmentTransaction { Id = 2, AccountId = 30, AssetListingId = 1, TradeDate = DateOnly.FromDateTime(day), Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 2, Currency = "PLN" });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        Authorize("owner", 641, UserRole.User);
        var client = new TransactionLogHttpClient(Client);
        var expected = (await client.GetLastTransactions(641, 100)).Where(e => e.Date >= day).ToList();
        Assert.Equal(7, expected.Count);
        var query = new TransactionLogQuery(DateTime.SpecifyKind(day, DateTimeKind.Unspecified), day.AddDays(1), pageSize);
        var page = await client.GetPage(641, query, ct);
        Assert.Equal(expected.Take(pageSize), page.Items);
        List<TransactionLogEntryDto> actual = [];
        for (var i = 0; ; i++)
        {
            Assert.True(i < 10, "Cursor did not advance.");
            actual.AddRange(page.Items);
            if (page.NextCursor is not TransactionLogCursor cursor)
                break;
            Assert.Equal(DateTimeKind.Utc, cursor.Date.Kind);
            page = await client.GetPage(641, query with { Cursor = cursor }, ct);
        }

        Assert.Equal(expected, actual);
        Assert.All(actual, e => Assert.Contains(e.AccountId, new[] { 10, 11, 20, 30 }));
        Assert.Equal(actual.Count, actual.Select(e => (e.AccountType, e.EntryId)).Distinct().Count());
        var intraday = await client.GetPage(641, new(day.AddHours(1), day.AddDays(1)), ct);
        Assert.Equal(3, Assert.Single(intraday.Items).EntryId);
        var empty = await client.GetPage(641, new(day.AddYears(1), day.AddYears(2)), ct);
        Assert.Empty(empty.Items);
        Assert.Null(empty.NextCursor);
    }

    public override void Dispose()
    {
        _database?.Dispose();
        base.Dispose();
    }
}