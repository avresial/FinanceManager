using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class AssetControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase.Context);
    }

    private static AssetDto SampleAsset() => new()
    {
        Name = "iShares Core S&P 500 UCITS ETF USD (Acc)",
        Type = AssetType.ETF,
        Isin = "IE00B5BMR087",
        Issuer = "iShares",
        BaseCurrency = "USD",
        DistributionPolicy = DistributionPolicy.Accumulating,
        BenchmarkIndex = "S&P 500",
        IsUcits = true
    };

    private async Task<AssetDto> CreateAssetAsAdmin()
    {
        Authorize("admin", 1, UserRole.Admin);
        var response = await Client.PostAsJsonAsync("api/admin/assets", SampleAsset(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetDto>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task CreateAsset_AsAdmin_PersistsAndIsRetrievable()
    {
        var created = await CreateAssetAsAdmin();

        Assert.True(created.Id > 0);

        var fetched = await Client.GetFromJsonAsync<AssetDto>($"api/admin/assets/{created.Id}", TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
        Assert.Equal("IE00B5BMR087", fetched!.Isin);
        Assert.Equal(AssetType.ETF, fetched.Type);
        Assert.Equal(DistributionPolicy.Accumulating, fetched.DistributionPolicy);
    }

    [Fact]
    public async Task CreateAsset_AsNonAdmin_IsForbidden()
    {
        Authorize("user", 2, UserRole.User);
        var response = await Client.PostAsJsonAsync("api/admin/assets", SampleAsset(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAssets_Anonymous_IsUnauthorized()
    {
        ClearAuthorization();
        var response = await Client.GetAsync("api/admin/assets", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Asset_WithMultipleListingsAndSymbol_IsReturnedAsTree()
    {
        var asset = await CreateAssetAsAdmin();

        await AddListing(asset.Id, "CSPX", "XLON", "USD", primary: true);
        await AddListing(asset.Id, "CSP1", "XLON", "GBX", priceMultiplier: 0.01m);
        var sxr8 = await AddListing(asset.Id, "SXR8", "XETR", "EUR");

        var symbolResponse = await Client.PostAsJsonAsync(
            $"api/admin/assets/listings/{sxr8.Id}/symbols",
            new MarketDataSymbolDto { Provider = MarketDataProvider.AlphaVantage, Symbol = "SXR8.DE", IsPrimary = true },
            TestContext.Current.CancellationToken);
        symbolResponse.EnsureSuccessStatusCode();

        var fetched = await Client.GetFromJsonAsync<AssetDto>($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
        Assert.Equal(3, fetched!.Listings.Count);

        var gbx = fetched.Listings.Single(l => l.Ticker == "CSP1");
        Assert.Equal(0.01m, gbx.PriceMultiplier);

        var xetr = fetched.Listings.Single(l => l.Ticker == "SXR8");
        Assert.Single(xetr.MarketDataSymbols);
        Assert.Equal("SXR8.DE", xetr.MarketDataSymbols[0].Symbol);
    }

    [Fact]
    public async Task UpdateAsset_ChangesEditableFields()
    {
        var asset = await CreateAssetAsAdmin();
        asset.Issuer = "BlackRock";

        var update = await Client.PutAsJsonAsync($"api/admin/assets/{asset.Id}", asset, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var fetched = await Client.GetFromJsonAsync<AssetDto>($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken);
        Assert.Equal("BlackRock", fetched!.Issuer);
    }

    [Fact]
    public async Task DeleteAsset_RemovesIt()
    {
        var asset = await CreateAssetAsAdmin();

        var delete = await Client.DeleteAsync($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var fetched = await Client.GetAsync($"api/admin/assets/{asset.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task CreateListing_WithNonPositiveMultiplier_IsBadRequest()
    {
        var asset = await CreateAssetAsAdmin();
        var response = await Client.PostAsJsonAsync(
            $"api/admin/assets/{asset.Id}/listings",
            new AssetListingDto { Ticker = "CSPX", ExchangeMic = "XLON", TradingCurrency = "USD", PriceMultiplier = 0m },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<AssetListingDto> AddListing(long assetId, string ticker, string mic, string currency,
        bool primary = false, decimal priceMultiplier = 1m)
    {
        var response = await Client.PostAsJsonAsync(
            $"api/admin/assets/{assetId}/listings",
            new AssetListingDto
            {
                Ticker = ticker,
                ExchangeMic = mic,
                ExchangeName = mic,
                TradingCurrency = currency,
                IsPrimaryListing = primary,
                PriceMultiplier = priceMultiplier
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetListingDto>(TestContext.Current.CancellationToken))!;
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        GC.SuppressFinalize(this);
    }
}