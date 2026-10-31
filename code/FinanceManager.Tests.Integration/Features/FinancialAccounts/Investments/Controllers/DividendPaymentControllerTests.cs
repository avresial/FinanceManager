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

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments.Controllers;

[Trait("Category", "Integration")]
public class DividendPaymentControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private TestDatabase? _database;
    private const string _route = "api/accounts/4/dividends";
    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);
    }
    private void Seed()
    {
        var context = _database!.Context;
        context.Accounts.Add(new FinancialAccountBaseDto { AccountId = 4, UserId = 1, Name = "Broker", AccountType = AccountType.Stock });
        context.Currencies.Add(new Currency(1, "USD", "$"));
        context.Assets.Add(new Asset { Id = 1, Name = "Test fund", Type = AssetType.ETF, Listings = [new() { Id = 8, Ticker = "TEST", ExchangeMic = "XNAS", TradingCurrency = "USD" }] });
        context.InvestmentTransactions.Add(new InvestmentTransaction { UserId = 1, AccountId = 4, AssetListingId = 8, Type = InvestmentTransactionType.Buy, Quantity = 3, UnitPrice = 100, Currency = "USD", TradeDate = new(2019, 1, 1) });
        context.SaveChanges();
    }
    private static DividendPaymentRequest Request(decimal amount = 12) => new() { AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = amount, Currency = "USD" };

    [Fact]
    public async Task OwnerCanCreateReadEditAndDelete_WithoutChangingTradesOrBalances()
    {
        Seed();
        Authorize("owner", 1, UserRole.User);
        using var added = await Client.PostAsJsonAsync(_route, Request(), TestContext.Current.CancellationToken);
        added.EnsureSuccessStatusCode();
        var id = await added.Content.ReadFromJsonAsync<long>(TestContext.Current.CancellationToken);
        var page = await Client.GetFromJsonAsync<DividendPaymentPage>(_route, TestContext.Current.CancellationToken);
        Assert.Equal(12m, Assert.Single(page!.Items).Amount);
        Assert.Equal("Test fund", page.Items[0].HoldingName);
        using var edited = await Client.PutAsJsonAsync($"{_route}/{id}", Request(25), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        Assert.Equal(25m, (await Client.GetFromJsonAsync<DividendPaymentPage>(_route, TestContext.Current.CancellationToken))!.Items[0].Amount);
        var trade = await _database!.Context.InvestmentTransactions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3m, trade.Quantity);
        Assert.Equal(100m, trade.UnitPrice);
        Assert.Empty(await _database.Context.CurrencyEntries.ToListAsync(TestContext.Current.CancellationToken));
        using var deleted = await Client.DeleteAsync($"{_route}/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<DividendPaymentPage>(_route, TestContext.Current.CancellationToken))!.Items);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousAndOtherUsersCannotReadOrWrite(bool authenticated)
    {
        Seed();
        if (authenticated) Authorize("other", 2, UserRole.User); else ClearAuthorization();
        var expected = authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        using var read = await Client.GetAsync(_route, TestContext.Current.CancellationToken);
        using var holdings = await Client.GetAsync(_route + "/holdings", TestContext.Current.CancellationToken);
        using var create = await Client.PostAsJsonAsync(_route, Request(), TestContext.Current.CancellationToken);
        using var update = await Client.PutAsJsonAsync(_route + "/1", Request(), TestContext.Current.CancellationToken);
        using var delete = await Client.DeleteAsync(_route + "/1", TestContext.Current.CancellationToken);
        Assert.All(new[] { read, holdings, create, update, delete }, x => Assert.Equal(expected, x.StatusCode));
    }
    [Fact]
    public async Task HistoryPagingHasStableTiesAndDoesNotLeakOtherUsersRecords()
    {
        Seed();
        Authorize("owner", 1, UserRole.User);
        for (var i = 1; i <= 3; i++)
        {
            using var response = await Client.PostAsJsonAsync(_route, Request(i), TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }
        _database!.Context.DividendPayments.Add(new DividendPayment { AccountId = 4, UserId = 2, AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 99, Currency = "USD" });
        await _database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var first = await Client.GetFromJsonAsync<DividendPaymentPage>(_route + "?pageSize=2", TestContext.Current.CancellationToken);
        var second = await Client.GetFromJsonAsync<DividendPaymentPage>(_route + "?pageSize=2&page=2", TestContext.Current.CancellationToken);
        Assert.Equal(3, first!.TotalCount);
        Assert.Equal(new[] { 3m, 2m, 1m }, first.Items.Concat(second!.Items).Select(x => x.Amount));
        Assert.Equal(3, first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count());
    }
    [Fact]
    public async Task InvalidPaymentAndPagingReturnActionableBadRequest()
    {
        Seed();
        Authorize("owner", 1, UserRole.User);
        using var invalid = await Client.PostAsJsonAsync(_route, Request(0), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("positive", await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var paging = await Client.GetAsync(_route + "?page=0", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, paging.StatusCode);
    }
}