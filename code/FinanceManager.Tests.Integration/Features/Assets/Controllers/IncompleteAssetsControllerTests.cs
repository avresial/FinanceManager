using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
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
public class IncompleteAssetsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private TestDatabase? _database;
    private const string _base = "api/admin/assets/incomplete";

    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);
        var resolver = new Mock<IMarketDataSymbolResolver>();
        resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.AlphaVantage);
        resolver.Setup(x => x.ValidateAsync(It.IsAny<Domain.Assets.Discovery.MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        services.RemoveAll<IMarketDataSymbolResolver>();
        services.AddSingleton(resolver.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Endpoints_RequireAdmin(bool authenticated)
    {
        if (authenticated) Authorize("user", 2, UserRole.User);
        else ClearAuthorization();
        var expected = authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        using var queue = await Client.GetAsync(_base, TestContext.Current.CancellationToken);
        Assert.Equal(expected, queue.StatusCode);
        using var repair = await Client.PostAsJsonAsync($"{_base}/listings/1/symbols", new MarketDataSymbolDto { Symbol = "TEST" }, TestContext.Current.CancellationToken);
        Assert.Equal(expected, repair.StatusCode);
    }

    [Fact]
    public async Task IncompleteListing_StaysUntilMetadataAndValidatedSymbolAreRepaired()
    {
        Authorize("admin", 1, UserRole.Admin);
        var asset = new Asset
        {
            Name = "Incomplete test fund",
            Isin = "IE0000000001",
            Type = AssetType.ETF,
            Listings = [new() { Ticker = "TEST", ExchangeMic = null, TradingCurrency = null }]
        };
        _database!.Context.Assets.Add(asset);
        await _database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single((await Queue()).Items);
        Assert.True(item.MissingVenue);
        Assert.True(item.MissingCurrency);
        Assert.True(item.NoEnabledSymbol);
        var listing = asset.Listings.Single();
        using var premature = await Client.PostAsJsonAsync($"{_base}/listings/{listing.Id}/symbols", new MarketDataSymbolDto { Symbol = "TEST" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, premature.StatusCode);
        var dto = listing.ToDto();
        dto.ExchangeMic = "XWAR";
        dto.TradingCurrency = "PLN";
        using var metadata = await Client.PutAsJsonAsync($"api/admin/assets/listings/{listing.Id}", dto, TestContext.Current.CancellationToken);
        metadata.EnsureSuccessStatusCode();
        Assert.True(Assert.Single((await Queue()).Items).NoEnabledSymbol);
        using var repair = await Client.PostAsJsonAsync($"{_base}/listings/{listing.Id}/symbols", new MarketDataSymbolDto { Symbol = "TEST", Provider = MarketDataProvider.AlphaVantage }, TestContext.Current.CancellationToken);
        repair.EnsureSuccessStatusCode();
        Assert.Empty((await Queue()).Items);
        var stored = await _database.Context.MarketDataSymbols.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(stored.LastSuccessfulPriceFetchAt);
        Assert.Null(stored.LastError);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=51")]
    public async Task InvalidPaging_ReturnsBadRequest(string query)
    {
        Authorize("admin", 1, UserRole.Admin);
        using var response = await Client.GetAsync(_base + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<IncompleteAssetPage> Queue() =>
        (await Client.GetFromJsonAsync<IncompleteAssetPage>(_base, TestContext.Current.CancellationToken))!;
}