using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
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

namespace FinanceManager.Tests.Integration.Features.Assets.Controllers;

[Trait("Category", "Integration")]
public class AdminAssetDiscoveryControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private static readonly Mock<IInvestmentInstrumentDiscoveryService> _discovery = new();
    private static readonly Mock<IMarketDataSymbolResolver> _resolver = new();
    private TestDatabase? _database;
    private const string _base = "api/admin/assets/discovery";

    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);
        _discovery.Reset();
        _resolver.Reset();
        _discovery.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([Instrument()]);
        _discovery.Setup(x => x.DiscoverListingsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Instrument(), Instrument("VWRA", "XLON", "USD", "BBGL2")]);
        _resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.TwelveData);
        _resolver.SetupGet(x => x.Priority).Returns(1);
        _resolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentDiscoveryResultDto instrument, CancellationToken _) =>
                new MarketDataSymbolResolution(MarketDataProvider.TwelveData, 1, "resolved", $"{instrument.Ticker}.TD", Currency: instrument.TradingCurrency));
        services.AddSingleton(_discovery.Object);
        services.RemoveAll<IMarketDataSymbolResolver>();
        services.AddSingleton(_resolver.Object);
    }

    private static InstrumentDiscoveryResultDto Instrument(string ticker = "VWCE", string mic = "XETR", string currency = "EUR", string figi = "BBGL1") => new()
    {
        DisplayName = "Vanguard FTSE All-World",
        SecurityType = "ETF",
        Isin = "IE00BK5BQT80",
        ShareClassFigi = "BBGSC",
        Ticker = ticker,
        ExchangeMic = mic,
        ExchangeName = mic,
        TradingCurrency = currency,
        ListingFigi = figi
    };

    private static AssetListingDto Listing(string ticker = "VWCE", string mic = "XETR", string currency = "EUR", string figi = "BBGL1") => new()
    {
        Ticker = ticker,
        ExchangeMic = mic,
        ExchangeName = mic,
        TradingCurrency = currency,
        ListingFigi = figi,
        MarketDataSymbols = [new() { Provider = MarketDataProvider.AlphaVantage, Symbol = $"{ticker}.AV", IsPrimary = true }]
    };

    private static AssetDto Draft() => new()
    {
        Name = "Reviewed fund name",
        Type = AssetType.ETF,
        Issuer = "Curated issuer",
        Isin = "IE00BK5BQT80",
        ShareClassFigi = "BBGSC",
        Listings = [Listing(), Listing("VWRA", "XLON", "USD", "BBGL2")]
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryEndpoint_RequiresAdmin(bool authenticated)
    {
        if (authenticated) Authorize("user", 2, UserRole.User);
        else ClearAuthorization();
        var expected = authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        using var search = await Client.GetAsync($"{_base}/search?query=VWCE", TestContext.Current.CancellationToken);
        Assert.Equal(expected, search.StatusCode);
        foreach (var path in new[] { "draft", "listings", "symbols", "create", "1/listings" })
        {
            using var response = await Client.PostAsJsonAsync($"{_base}/{path}", new { }, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
        }
        _discovery.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAndDraft_AsAdmin_ReturnAllVenueChoicesWithoutPersisting()
    {
        Authorize("admin", 1, UserRole.Admin);
        var results = await Client.GetFromJsonAsync<List<InstrumentDiscoveryResultDto>>($"{_base}/search?query=VWCE", TestContext.Current.CancellationToken);
        using var response = await Client.PostAsJsonAsync($"{_base}/draft", Assert.Single(results!), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var draft = await response.Content.ReadFromJsonAsync<AdminAssetDraftDto>(TestContext.Current.CancellationToken);
        Assert.Equal(2, draft!.Listings.Count);
        Assert.Null(draft.Asset.BaseCurrency);
        Assert.Equal("BBGSC", draft.Asset.ShareClassFigi);
        Assert.Empty((await Client.GetFromJsonAsync<List<AssetDto>>("api/admin/assets", TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Create_SavesReviewedAssetListingsAndSymbolsTogether()
    {
        var asset = await Create(Draft());
        var stored = await Client.GetFromJsonAsync<AssetDto>($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken);
        Assert.Equal("Reviewed fund name", stored!.Name);
        Assert.Equal("Curated issuer", stored.Issuer);
        Assert.Equal(2, stored.Listings.Count);
        Assert.All(stored.Listings, listing =>
        {
            Assert.True(listing.Id > 0);
            Assert.Equal(stored.Id, listing.AssetId);
            Assert.True(Assert.Single(listing.MarketDataSymbols).Id > 0);
            Assert.Equal(listing.Id, listing.MarketDataSymbols.Single().AssetListingId);
        });
    }

    [Fact]
    public async Task InvalidLaterListing_LeavesNoPartiallyCreatedAsset()
    {
        Authorize("admin", 1, UserRole.Admin);
        var draft = Draft();
        draft.Listings[1].TradingCurrency = "UNKNOWN";
        using var response = await Client.PostAsJsonAsync($"{_base}/create", draft, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<List<AssetDto>>("api/admin/assets", TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task ExistingAsset_AddsMissingListingAndSymbolWithoutOverwritingCuratedData()
    {
        var draft = Draft();
        draft.Listings.RemoveAt(0);
        draft.Listings[0].MarketDataSymbols[0].IsEnabled = false;
        var asset = await Create(draft);
        using var find = await Client.PostAsJsonAsync($"{_base}/listings", asset, TestContext.Current.CancellationToken);
        find.EnsureSuccessStatusCode();
        Assert.Equal("VWCE", Assert.Single((await find.Content.ReadFromJsonAsync<List<InstrumentDiscoveryResultDto>>(TestContext.Current.CancellationToken))!).Ticker);
        using var add = await Client.PostAsJsonAsync($"{_base}/{asset.Id}/listings", Listing(), TestContext.Current.CancellationToken);
        add.EnsureSuccessStatusCode();
        var oldListing = asset.Listings.Single();
        using var symbols = await Client.PostAsJsonAsync($"{_base}/symbols?listingId={oldListing.Id}", Instrument("VWRA", "XLON", "USD", "BBGL2"), TestContext.Current.CancellationToken);
        symbols.EnsureSuccessStatusCode();
        var resolved = Assert.Single((await symbols.Content.ReadFromJsonAsync<List<MarketDataSymbolResolution>>(TestContext.Current.CancellationToken))!);
        using var useSymbol = await Client.PostAsJsonAsync($"api/admin/assets/listings/{oldListing.Id}/symbols",
            new MarketDataSymbolDto { Provider = resolved.Provider, Symbol = resolved.Symbol!, Currency = resolved.Currency }, TestContext.Current.CancellationToken);
        useSymbol.EnsureSuccessStatusCode();
        var stored = (await Client.GetFromJsonAsync<AssetDto>($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken))!;
        Assert.Equal("Reviewed fund name", stored.Name);
        Assert.Equal("Curated issuer", stored.Issuer);
        Assert.Equal(2, stored.Listings.Count);
        var oldSymbol = stored.Listings.Single(x => x.Id == oldListing.Id).MarketDataSymbols.Single(x => x.Provider == MarketDataProvider.AlphaVantage);
        Assert.False(oldSymbol.IsEnabled);
        Assert.True(oldSymbol.IsPrimary);
        Assert.Contains(stored.Listings.Single(x => x.Id == oldListing.Id).MarketDataSymbols, x => x.Provider == MarketDataProvider.TwelveData);
    }

    [Fact]
    public async Task FindSymbols_RejectsUnsavedListingIdentityEvenWhenFigiMatches()
    {
        var asset = await Create(Draft());
        var listing = asset.Listings.Single(x => x.Ticker == "VWCE");
        using var response = await Client.PostAsJsonAsync($"{_base}/symbols?listingId={listing.Id}", Instrument(ticker: "CHANGED"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<AssetDto> Create(AssetDto draft)
    {
        Authorize("admin", 1, UserRole.Admin);
        using var response = await Client.PostAsJsonAsync($"{_base}/create", draft, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetDto>(TestContext.Current.CancellationToken))!;
    }

    public override void Dispose()
    {
        _database?.Dispose();
        base.Dispose();
    }
}