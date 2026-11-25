using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Transactions.Investments.Controllers;

[Trait("Category", "Integration")]
public class OwnerInvestmentTradesControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _route = "api/transactions/investment";
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -7;
    private const int _accountId = 40;
    private const long _listing = 8;
    private const long _sameTickerListing = 9;
    private const long _flatListing = 10;
    // The API host is reused across test instances, so it keeps the first instance's mocks: they must be static and reset per instance.
    private static readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private static readonly Mock<ICacheInvalidator> _cacheInvalidator = new();
    private TestDatabase? _database;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _priceProvider.Reset();
        _cacheInvalidator.Reset();
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);
        services.RemoveAll<IInvestmentPriceProvider>();
        services.AddSingleton(_priceProvider.Object);
        services.RemoveAll<ICacheInvalidator>();
        services.AddSingleton(_cacheInvalidator.Object);
    }

    private AppDbContext Db => _database!.Context;

    private void Seed()
    {
        Db.Accounts.Add(new FinancialAccountBaseDto { AccountId = _accountId, UserId = _owner, Name = "Broker", AccountType = AccountType.Stock });
        Db.Currencies.Add(new Currency(1, "USD", "$"));
        Db.Assets.Add(new Asset
        {
            Id = 1,
            Name = "Test fund",
            Type = AssetType.ETF,
            Listings =
            [
                new() { Id = _listing, Ticker = "TEST", ExchangeMic = "XNAS", ExchangeName = "Nasdaq", TradingCurrency = "USD" },
                new() { Id = _sameTickerListing, Ticker = "TEST", ExchangeMic = "XLON", ExchangeName = "London", TradingCurrency = "USD" },
                new() { Id = _flatListing, Ticker = "FLAT", ExchangeMic = "XNAS", ExchangeName = "Nasdaq", TradingCurrency = "USD" }
            ]
        });
        Db.SaveChanges();
    }

    private async Task<long> SeedTrade(int userId, int? accountId, InvestmentTransactionType type, decimal quantity, DateOnly date,
        decimal price = 100m, long listing = _listing)
    {
        var trade = new InvestmentTransaction
        {
            UserId = userId,
            AccountId = accountId,
            AssetListingId = listing,
            Type = type,
            Quantity = quantity,
            UnitPrice = price,
            Currency = "USD",
            TradeDate = date
        };
        Db.InvestmentTransactions.Add(trade);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return trade.EntryId;
    }

    private static CreateInvestmentTradeRequest Create(decimal quantity = 3m, long listing = _listing, IReadOnlyCollection<int>? labels = null) =>
        new(listing, InvestmentTransactionTypeDto.Buy, quantity, 120m, "USD", new DateOnly(2024, 6, 1), LabelIds: labels);

    private static UpdateInvestmentTradeRequest Update(decimal quantity = 5m, IReadOnlyCollection<int>? labels = null) =>
        new(_listing, InvestmentTransactionTypeDto.Buy, quantity, 130m, "USD", new DateOnly(2024, 6, 2), LabelIds: labels);

    [Fact]
    public async Task Anonymous_IsUnauthorizedOnEveryRoute()
    {
        Seed();
        ClearAuthorization();
        var ct = TestContext.Current.CancellationToken;
        var responses = new[]
        {
            await Client.GetAsync(_route, ct),
            await Client.GetAsync(_route + "/holdings", ct),
            await Client.GetAsync(_route + "/1", ct),
            await Client.PostAsJsonAsync(_route, Create(), ct),
            await Client.PutAsJsonAsync(_route + "/1", Update(), ct),
            await Client.DeleteAsync(_route + "/1", ct)
        };
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
    }

    [Fact]
    public async Task NullLinkTrade_CanBeCreatedReadUpdatedAndDeleted_WithoutAnAccount()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync(_route, Create(), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<InvestmentTransactionDto>(ct);
        Assert.NotNull(dto);
        Assert.EndsWith($"{_route}/{dto.Id}", created.Headers.Location!.AbsolutePath);
        var stored = await Db.InvestmentTransactions.SingleAsync(ct);
        Assert.Equal(_owner, stored.UserId);
        Assert.Null(stored.AccountId);

        var read = await Client.GetFromJsonAsync<InvestmentTransactionDto>($"{_route}/{dto.Id}", ct);
        Assert.Equal(3m, read!.Quantity);

        using var updated = await Client.PutAsJsonAsync($"{_route}/{dto.Id}", Update(), ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Db.ChangeTracker.Clear();
        stored = await Db.InvestmentTransactions.SingleAsync(ct);
        Assert.Equal(5m, stored.Quantity);
        Assert.Equal(130m, stored.UnitPrice);
        Assert.Equal(_owner, stored.UserId);
        Assert.Null(stored.AccountId);

        using var deleted = await Client.DeleteAsync($"{_route}/{dto.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await Db.InvestmentTransactions.ToListAsync(ct));
        using var again = await Client.DeleteAsync($"{_route}/{dto.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task OwnerAndAccountInRequestBody_AreIgnored()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync(_route, new
        {
            userId = _other,
            accountId = _accountId,
            assetListingId = _listing,
            type = 0,
            quantity = 1m,
            unitPrice = 10m,
            currency = "USD",
            tradeDate = "2024-06-01"
        }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var stored = await Db.InvestmentTransactions.SingleAsync(ct);
        Assert.Equal(_owner, stored.UserId);
        Assert.Null(stored.AccountId);
    }

    [Fact]
    public async Task ForeignAndMissingIds_ReturnTheSame404_AndLeaveTheTradeUntouched()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1));
        Authorize("other", _other, UserRole.User);

        foreach (var target in new[] { id, id + 1000 })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"{_route}/{target}", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync($"{_route}/{target}", Update(), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"{_route}/{target}", ct)).StatusCode);
        }

        var page = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>(_route, ct);
        Assert.Empty(page!.Items);
        Assert.Empty((await Client.GetFromJsonAsync<List<InvestmentHoldingDto>>(_route + "/holdings", ct))!);
        Db.ChangeTracker.Clear();
        var stored = await Db.InvestmentTransactions.SingleAsync(ct);
        Assert.Equal(2m, stored.Quantity);
        Assert.Equal(_owner, stored.UserId);
    }

    [Fact]
    public async Task GuestWithNegativeId_OwnsItsTrades()
    {
        Seed();
        Authorize("guest", _guest, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync(_route, Create(), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(_guest, (await Db.InvestmentTransactions.SingleAsync(ct)).UserId);
        var page = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>(_route, ct);
        Assert.Single(page!.Items);
        Assert.Equal(3m, Assert.Single((await Client.GetFromJsonAsync<List<InvestmentHoldingDto>>(_route + "/holdings", ct))!).Units);
    }

    [Fact]
    public async Task Labels_OmittedKeeps_EmptyClears_UnknownIsRejected()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var label = new FinancialLabel { Name = "Long term" };
        Db.FinancialLabels.Add(label);
        await Db.SaveChangesAsync(ct);
        Authorize("owner", _owner, UserRole.User);

        var created = await (await Client.PostAsJsonAsync(_route, Create(labels: [label.Id]), ct)).Content.ReadFromJsonAsync<InvestmentTransactionDto>(ct);
        Assert.Equal(label.Id, Assert.Single(created!.Labels!).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{created.Id}", Update(labels: null), ct)).StatusCode);
        Assert.Equal(label.Id, Assert.Single((await Client.GetFromJsonAsync<InvestmentTransactionDto>($"{_route}/{created.Id}", ct))!.Labels!).Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"{_route}/{created.Id}", Update(labels: [int.MaxValue]), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync(_route, Create(labels: [int.MaxValue]), ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{created.Id}", Update(labels: []), ct)).StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<InvestmentTransactionDto>($"{_route}/{created.Id}", ct))!.Labels!);
    }

    [Fact]
    public async Task InvalidInput_IsRejected()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 1m, new(2024, 1, 1));

        var mismatch = await Client.PostAsJsonAsync(_route, Create() with { Currency = "EUR" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains("currency_mismatch", await mismatch.Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync(_route, Create(quantity: 0m), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync(_route, Create() with { Type = (InvestmentTransactionTypeDto)9 }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"{_route}/{id}", Update(quantity: 0m), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"{_route}/{id}", Update() with { AssetListingId = 404 }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"{_route}/{id}", Update() with { Currency = "USDX" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync($"{_route}/{id}", Update() with { Notes = new string('n', 513) }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{_route}?cursor=nope", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{_route}?pageSize=0", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{_route}?pageSize=101", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{_route}?type=7", ct)).StatusCode);
    }

    [Fact]
    public async Task UserIdZero_IsForbiddenOnEveryRoute_AndNeverInvalidatesCache()
    {
        Seed();
        Authorize("zero", 0, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var responses = new[]
        {
            await Client.GetAsync(_route, ct),
            await Client.GetAsync(_route + "/holdings", ct),
            await Client.GetAsync(_route + "/1", ct),
            await Client.PostAsJsonAsync(_route, Create(), ct),
            await Client.PutAsJsonAsync(_route + "/1", Update(), ct),
            await Client.DeleteAsync(_route + "/1", ct)
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Forbidden, x.StatusCode));
        Assert.Empty(await Db.InvestmentTransactions.ToListAsync(ct));
        _cacheInvalidator.Verify(x => x.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAndDelete_InvalidateOwnerCache_OnlyWhenTheTradeIsChanged()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1));
        Authorize("owner", _owner, UserRole.User);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync($"{_route}/{id + 1000}", Update(), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"{_route}/{id + 1000}", ct)).StatusCode);
        _cacheInvalidator.Verify(x => x.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{id}", Update(), ct)).StatusCode);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_owner, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"{_route}/{id}", ct)).StatusCode);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_owner, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAndDelete_ForForeignTrade_DoNotInvalidateAnyCache()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1));
        Authorize("other", _other, UserRole.User);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync($"{_route}/{id}", Update(), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"{_route}/{id}", ct)).StatusCode);
        _cacheInvalidator.Verify(x => x.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAndDelete_StillSucceed_WhenCacheInvalidationFails()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1));
        _cacheInvalidator.Setup(x => x.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("cache down"));
        Authorize("owner", _owner, UserRole.User);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{id}", Update(), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"{_route}/{id}", ct)).StatusCode);
        Db.ChangeTracker.Clear();
        Assert.Empty(await Db.InvestmentTransactions.ToListAsync(ct));
    }

    [Fact]
    public async Task LinkedTradeEditedByOwnerRoute_ChangesQuantityAndPreservesOtherTrades()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var first = await SeedTrade(_owner, _accountId, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1));
        var second = await SeedTrade(_owner, _accountId, InvestmentTransactionType.Buy, 3m, new(2024, 2, 1));
        Authorize("owner", _owner, UserRole.User);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"{_route}/{first}", Update(quantity: 10m) with { TradeDate = new(2024, 1, 1) }, ct)).StatusCode);

        Db.ChangeTracker.Clear();
        var rows = await Db.InvestmentTransactions.OrderBy(x => x.EntryId).ToListAsync(ct);
        Assert.All(rows, x => Assert.Equal(_accountId, x.AccountId));
        Assert.Equal([10m, 3m], rows.Select(x => x.Quantity));

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"{_route}/{first}", ct)).StatusCode);
        Db.ChangeTracker.Clear();
        Assert.Equal(3m, (await Db.InvestmentTransactions.SingleAsync(x => x.EntryId == second, ct)).Quantity);
    }

    [Fact]
    public async Task History_PagesThroughTiedDates_AndFiltersByListingAndType()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var day = new DateOnly(2024, 3, 1);
        var ids = new List<long>
        {
            await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 1m, day),
            await SeedTrade(_owner, _accountId, InvestmentTransactionType.Sell, 1m, day),
            await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 1m, day, listing: _sameTickerListing),
            await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 1m, new(2024, 1, 1))
        };
        await SeedTrade(_other, null, InvestmentTransactionType.Buy, 1m, day);
        Authorize("owner", _owner, UserRole.User);

        var seen = new List<long>();
        string? cursor = null;
        do
        {
            var url = cursor is null ? $"{_route}?pageSize=2" : $"{_route}?pageSize=2&cursor={Uri.EscapeDataString(cursor)}";
            var page = (await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>(url, ct))!;
            Assert.True(page.Items.Count <= 2);
            seen.AddRange(page.Items.Select(x => x.Id));
            cursor = page.HasMore ? page.NextCursor : null;
        } while (cursor is not null);

        Assert.Equal([ids[2], ids[1], ids[0], ids[3]], seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());

        var byListing = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>($"{_route}?assetListingId={_sameTickerListing}", ct);
        Assert.Equal(ids[2], Assert.Single(byListing!.Items).Id);
        var sells = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>($"{_route}?type=1", ct);
        Assert.Equal(ids[1], Assert.Single(sells!.Items).Id);
        var ranged = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>($"{_route}?startDate=2024-02-01&endDate=2024-12-31", ct);
        Assert.Equal(3, ranged!.Items.Count);
    }

    [Fact]
    public async Task Holdings_PoolAcrossAccounts_KeepNegative_OmitZero_AndIncludeUnpriced()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        await SeedTrade(_owner, _accountId, InvestmentTransactionType.Buy, 5m, new(2024, 1, 1));
        await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 2, 1), price: 0m);
        await SeedTrade(_owner, null, InvestmentTransactionType.Sell, 1m, new(2024, 3, 1));
        await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 1m, new(2024, 1, 1), listing: _sameTickerListing);
        await SeedTrade(_owner, null, InvestmentTransactionType.Sell, 3m, new(2024, 2, 1), listing: _sameTickerListing);
        await SeedTrade(_owner, _accountId, InvestmentTransactionType.Buy, 4m, new(2024, 1, 1), listing: _flatListing);
        await SeedTrade(_owner, null, InvestmentTransactionType.Sell, 4m, new(2024, 2, 1), listing: _flatListing);
        await SeedTrade(_other, null, InvestmentTransactionType.Buy, 50m, new(2024, 1, 1));
        Authorize("owner", _owner, UserRole.User);

        var holdings = await Client.GetFromJsonAsync<List<InvestmentHoldingDto>>($"{_route}/holdings?asOf=2024-12-31", ct);
        var main = Assert.Single(holdings!, x => x.AssetListingId == _listing);
        Assert.Equal(6m, main.Units);
        Assert.Equal("TEST", main.Ticker);
        Assert.Equal(AssetTypeDto.ETF, main.AssetType);
        var oversold = Assert.Single(holdings!, x => x.AssetListingId == _sameTickerListing);
        Assert.Equal(-2m, oversold.Units);
        Assert.Equal("TEST", oversold.Ticker);
        Assert.DoesNotContain(holdings!, x => x.AssetListingId == _flatListing);
        Assert.Equal(new long[] { _listing, _sameTickerListing }, holdings!.Select(x => x.AssetListingId));

        var early = await Client.GetFromJsonAsync<List<InvestmentHoldingDto>>($"{_route}/holdings?asOf=2024-01-15", ct);
        Assert.Equal([5m, 1m, 4m], early!.Select(x => x.Units));
    }

    [Fact]
    public async Task Holdings_SelectLatestOwnerTradeByDateThenId_AndRecoverOnlyMissingPrices()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var date = new DateOnly(2024, 5, 2);
        await SeedTrade(_guest, null, InvestmentTransactionType.Buy, 1m, date, price: 10m);
        await SeedTrade(_guest, null, InvestmentTransactionType.Buy, 1m, date, price: 0m);
        await SeedTrade(_other, null, InvestmentTransactionType.Buy, 1m, date, price: 200m);
        await SeedTrade(_guest, null, InvestmentTransactionType.Buy, 1m, date.AddDays(1), price: 300m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(_listing, It.IsAny<Currency>(), date.ToDateTime(TimeOnly.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(15m);
        Authorize("guest", _guest, UserRole.User);

        var row = Assert.Single((await Client.GetFromJsonAsync<List<InvestmentHoldingDto>>($"{_route}/holdings?asOf=2024-05-02", ct))!);
        Assert.Equal((2m, "USD", 15m), (row.Units, row.Currency, row.LatestUnitPrice));
        Db.ChangeTracker.Clear();
        Assert.Equal([10m, 15m, 200m, 300m], await Db.InvestmentTransactions.OrderBy(t => t.EntryId).Select(t => t.UnitPrice).ToListAsync(ct));
    }

    [Fact]
    public async Task MissingPrice_IsRecoveredOnRead_ForUnlinkedTrades()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedTrade(_owner, null, InvestmentTransactionType.Buy, 2m, new(2024, 1, 1), price: 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listing, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(150m);
        Authorize("owner", _owner, UserRole.User);

        var read = await Client.GetFromJsonAsync<InvestmentTransactionDto>($"{_route}/{id}", ct);
        Assert.Equal(150m, read!.UnitPrice);
        Db.ChangeTracker.Clear();
        Assert.Equal(150m, (await Db.InvestmentTransactions.SingleAsync(ct)).UnitPrice);
    }
}