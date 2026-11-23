using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Portfolio.Dividends.Controllers;

[Trait("Category", "Integration")]
public class OwnerDividendsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _route = "api/dividends";
    private const int _accountId = 4;
    private TestDatabase? _database;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);
    }

    private AppDbContext Db => _database!.Context;

    private void Seed()
    {
        Db.Accounts.Add(new FinancialAccountBaseDto { AccountId = _accountId, UserId = 1, Name = "Broker", AccountType = AccountType.Stock });
        Db.Currencies.Add(new Currency(1, "USD", "$"));
        Db.Assets.Add(new Asset
        {
            Id = 1,
            Name = "Test fund",
            Type = AssetType.ETF,
            Listings =
            [
                new() { Id = 8, Ticker = "TEST", ExchangeMic = "XNAS", TradingCurrency = "USD" },
                new() { Id = 9, Ticker = "OTHR", ExchangeMic = "XNAS", TradingCurrency = "USD" }
            ]
        });
        Db.InvestmentTransactions.Add(new InvestmentTransaction { UserId = 1, AccountId = _accountId, AssetListingId = 8, Type = InvestmentTransactionType.Buy, Quantity = 3, UnitPrice = 100, Currency = "USD", TradeDate = new(2019, 1, 1) });
        Db.SaveChanges();
    }

    private static DividendPaymentRequest Request(decimal amount = 12, long listing = 8) =>
        new() { AssetListingId = listing, PaymentDate = new(2020, 1, 1), Amount = amount, Currency = "USD" };

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        Seed();
        ClearAuthorization();
        var ct = TestContext.Current.CancellationToken;
        var responses = new[]
        {
            await Client.GetAsync(_route, ct),
            await Client.GetAsync(_route + "/holdings", ct),
            await Client.PostAsJsonAsync(_route, Request(), ct),
            await Client.PutAsJsonAsync(_route + "/1", Request(), ct),
            await Client.DeleteAsync(_route + "/1", ct)
        };
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
    }

    [Fact]
    public async Task OwnerWidePayment_IsUnlinked_AndCanBeListedEditedAndDeleted()
    {
        Seed();
        Authorize("owner", 1, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var added = await Client.PostAsJsonAsync(_route, Request(), ct);
        added.EnsureSuccessStatusCode();
        var id = await added.Content.ReadFromJsonAsync<long>(ct);
        var stored = await Db.DividendPayments.SingleAsync(ct);
        Assert.Equal(1, stored.UserId);
        Assert.Null(stored.AccountId);

        var page = await Client.GetFromJsonAsync<DividendPaymentPage>(_route, ct);
        var item = Assert.Single(page!.Items);
        Assert.Null(item.AccountId);
        Assert.Equal("Test fund", item.HoldingName);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{id}", Request(25), ct)).StatusCode);
        Assert.Equal(25m, (await Client.GetFromJsonAsync<DividendPaymentPage>(_route, ct))!.Items[0].Amount);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"{_route}/{id}", ct)).StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<DividendPaymentPage>(_route, ct))!.Items);
    }

    [Fact]
    public async Task AssetsNavigation_IncludesOwnersWithOnlyDividends()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        Db.DividendPayments.Add(new DividendPayment { UserId = -2, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 1, Currency = "USD" });
        await Db.SaveChangesAsync(ct);
        Authorize("guest", -2, UserRole.User);
        Assert.True(await Client.GetFromJsonAsync<bool>("api/Assets/IsAnyAccountWithAssets/-2", ct));
        Authorize("other", -3, UserRole.User);
        Assert.False(await Client.GetFromJsonAsync<bool>("api/Assets/IsAnyAccountWithAssets/-3", ct));
    }

    [Fact]
    public async Task History_CoversAccountLinkedAndUnlinkedPayments_ButNotOtherOwners()
    {
        Seed();
        Db.DividendPayments.AddRange(
            new DividendPayment { UserId = 1, AccountId = _accountId, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 1, Currency = "USD" },
            new DividendPayment { UserId = 1, AccountId = null, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 2, Currency = "USD" },
            new DividendPayment { UserId = 2, AccountId = null, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 99, Currency = "USD" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("owner", 1, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        var first = await Client.GetFromJsonAsync<DividendPaymentPage>(_route + "?pageSize=1", ct);
        var second = await Client.GetFromJsonAsync<DividendPaymentPage>(_route + "?pageSize=1&page=2", ct);
        Assert.Equal(2, first!.TotalCount);
        Assert.Equal([2m, 1m], new[] { first.Items[0].Amount, second!.Items[0].Amount });
    }

    [Fact]
    public async Task ForeignAndMissingIds_Return404()
    {
        Seed();
        var foreign = new DividendPayment { UserId = 2, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 5, Currency = "USD" };
        Db.DividendPayments.Add(foreign);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("owner", 1, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        foreach (var id in new[] { foreign.Id, foreign.Id + 1000 })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync($"{_route}/{id}", Request(), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"{_route}/{id}", ct)).StatusCode);
        }
        Db.ChangeTracker.Clear();
        Assert.Equal(5m, (await Db.DividendPayments.SingleAsync(ct)).Amount);
    }

    [Fact]
    public async Task Guest_WithNegativeId_OwnsItsPayments()
    {
        Seed();
        Db.InvestmentTransactions.Add(new InvestmentTransaction { UserId = -7, AssetListingId = 8, Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 1, Currency = "USD", TradeDate = new(2019, 1, 1) });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("guest", -7, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        (await Client.PostAsJsonAsync(_route, Request(), ct)).EnsureSuccessStatusCode();
        Assert.Equal(-7, (await Db.DividendPayments.SingleAsync(ct)).UserId);
        Assert.Single((await Client.GetFromJsonAsync<DividendPaymentPage>(_route, ct))!.Items);
    }

    [Fact]
    public async Task Holdings_ListOwnerWideChoices_IncludingSoldOutAndPriorPayouts_ButNotOthers()
    {
        Seed();
        Db.InvestmentTransactions.Add(new InvestmentTransaction { UserId = 1, AssetListingId = 8, Type = InvestmentTransactionType.Sell, Quantity = 3, UnitPrice = 100, Currency = "USD", TradeDate = new(2019, 6, 1) });
        Db.DividendPayments.Add(new DividendPayment { UserId = 2, AssetListingId = 9, PaymentDate = new(2020, 1, 1), Amount = 1, Currency = "USD" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("owner", 1, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        var holdings = await Client.GetFromJsonAsync<List<DividendHoldingOption>>(_route + "/holdings", ct);
        Assert.Equal(8, Assert.Single(holdings!).AssetListingId);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync(_route, Request(listing: 9), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync(_route, Request(), ct)).StatusCode);
    }

    [Fact]
    public async Task UnlinkedPayment_SurvivesDeletingTheAccount_ButLinkedOnesDoNot()
    {
        Seed();
        Db.DividendPayments.Add(new DividendPayment { UserId = 1, AccountId = _accountId, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 1, Currency = "USD" });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("owner", 1, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        (await Client.PostAsJsonAsync(_route, Request(7), ct)).EnsureSuccessStatusCode();

        using var removed = await Client.DeleteAsync($"api/InvestmentAccount/Delete/{_accountId}", ct);
        removed.EnsureSuccessStatusCode();

        Db.ChangeTracker.Clear();
        var remaining = await Db.DividendPayments.SingleAsync(ct);
        Assert.Equal(7m, remaining.Amount);
        Assert.Null(remaining.AccountId);
    }
}