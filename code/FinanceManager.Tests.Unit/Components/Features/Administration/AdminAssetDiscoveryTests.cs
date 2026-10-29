using Bunit;
using FinanceManager.Components.Features.Administration.Components;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration;

[Trait("Category", "Unit")]
public class AdminAssetDiscoveryTests
{
    private static InstrumentDiscoveryResultDto Instrument(string ticker = "VWCE", string mic = "XETR", string currency = "EUR") => new()
    {
        DisplayName = "External fund name",
        Ticker = ticker,
        ExchangeMic = mic,
        ExchangeName = mic,
        TradingCurrency = currency,
        Isin = "IE00BK5BQT80",
        ShareClassFigi = "BBGSC",
        SecurityType = "ETF"
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static BunitContext Context(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new AssetHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }, NullLogger<AssetHttpClient>.Instance));
        context.Render<MudPopoverProvider>();
        return context;
    }

    [Fact]
    public async Task Refresh_LeavesCuratedValuesUntouchedUntilEachFieldIsAccepted()
    {
        var writes = 0;
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
                return Task.FromResult(Json(new[] { Instrument() }));
            if (request.RequestUri.AbsolutePath.EndsWith("/draft", StringComparison.Ordinal))
                return Task.FromResult(Json(new AdminAssetDraftDto { Asset = new() { Name = "External fund name", Type = AssetType.ETF, Isin = "IE00BK5BQT80", ShareClassFigi = "BBGSC" } }));
            writes++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        await using var context = Context(handler);
        var asset = new AssetDto { Id = 10, Name = "Curated name", Type = AssetType.Stock, Issuer = "Manual issuer", BaseCurrency = "USD", Isin = "IE00BK5BQT80", ShareClassFigi = "BBGSC" };
        var cut = context.Render<AdminAssetDiscovery>(p => p.Add(x => x.Asset, asset));
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Refresh from external sources").ClickAsync(new MouseEventArgs());
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Review fields").ClickAsync(new MouseEventArgs());

        Assert.Equal("Curated name", asset.Name);
        Assert.Equal(AssetType.Stock, asset.Type);
        var row = cut.FindAll("tr").Single(x => x.TextContent.Contains("External fund name", StringComparison.Ordinal));
        await row.QuerySelector("button")!.ClickAsync(new MouseEventArgs());

        Assert.Equal("External fund name", asset.Name);
        Assert.Equal(AssetType.Stock, asset.Type);
        Assert.Equal("Manual issuer", asset.Issuer);
        Assert.Equal("USD", asset.BaseCurrency);
        Assert.Equal(0, writes);
    }

    [Fact]
    public async Task NewAsset_DefaultListingAndSelectedExtraVenueResolveIntoOneEditableDraft()
    {
        using var handler = new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)) return Json(new[] { Instrument() });
            if (request.RequestUri.AbsolutePath.EndsWith("/draft", StringComparison.Ordinal))
                return Json(new AdminAssetDraftDto
                {
                    Asset = new() { Name = "External fund name", Type = AssetType.ETF, Isin = "IE00BK5BQT80", ShareClassFigi = "BBGSC" },
                    Listings = [Instrument(), Instrument("VWRA", "XLON", "USD")]
                });
            var instrument = (await request.Content!.ReadFromJsonAsync<InstrumentDiscoveryResultDto>(ct))!;
            return Json(new[] { new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 1, "resolved", $"{instrument.Ticker}.AV") });
        });
        await using var context = Context(handler);
        var asset = new AssetDto();
        var cut = context.Render<AdminAssetDiscovery>(p => p.Add(x => x.Asset, asset).Add(x => x.IsNew, true).Add(x => x.Currencies, ["EUR", "USD"]));
        cut.Find("input").Input("VWCE");
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Use result").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Single(Assert.Single(asset.Listings).MarketDataSymbols));
        Assert.Equal("VWCE", asset.Listings[0].Ticker);
        Assert.Equal("External fund name", asset.Name);
        Assert.Null(asset.BaseCurrency);

        var extra = cut.FindComponents<MudCheckBox<bool>>().Single(x => x.Instance.Label == "VWRA · XLON · USD");
        await extra.InvokeAsync(() => extra.Instance.ValueChanged.InvokeAsync(true));
        cut.WaitForAssertion(() => Assert.Equal(2, asset.Listings.Count));
        cut.WaitForAssertion(() => Assert.Single(asset.Listings.Single(x => x.Ticker == "VWRA").MarketDataSymbols));
        Assert.Equal("VWRA.AV", asset.Listings.Single(x => x.Ticker == "VWRA").MarketDataSymbols[0].Symbol);

        cut.Render(p => p.Add(x => x.Asset, new AssetDto { Id = 10, Name = "Saved fund" }).Add(x => x.IsNew, false));
        Assert.DoesNotContain("Review the asset fields and selected listings", cut.Markup);
        Assert.DoesNotContain("Provider symbols to create", cut.Markup);
    }

    [Fact]
    public async Task UnrecognizedVenuesStayCollapsedUntilRevealed()
    {
        var unknownVenue = new InstrumentDiscoveryResultDto
        {
            DisplayName = "External fund name",
            Ticker = "VWCEOTC",
            ExchangeCode = "ER",
            ExchangeName = "ER",
            Isin = "IE00BK5BQT80",
            ShareClassFigi = "BBGSC",
            SecurityType = "ETF"
        };
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)) return Task.FromResult(Json(new[] { Instrument() }));
            if (request.RequestUri.AbsolutePath.EndsWith("/draft", StringComparison.Ordinal))
                return Task.FromResult(Json(new AdminAssetDraftDto
                {
                    Asset = new() { Name = "External fund name", Type = AssetType.ETF, Isin = "IE00BK5BQT80", ShareClassFigi = "BBGSC" },
                    Listings = [Instrument(), unknownVenue]
                }));
            return Task.FromResult(Json(Array.Empty<MarketDataSymbolResolution>()));
        });
        await using var context = Context(handler);
        var cut = context.Render<AdminAssetDiscovery>(p => p.Add(x => x.Asset, new AssetDto()).Add(x => x.IsNew, true));
        cut.Find("input").Input("VWCE");
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Use result").ClickAsync(new MouseEventArgs());

        Assert.DoesNotContain("VWCEOTC", cut.Markup);
        await cut.FindAll("button").Single(x => x.TextContent.Contains("Show 1 listings on unrecognized venues", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());
        Assert.Contains("VWCEOTC", cut.Markup);
    }

    [Fact]
    public async Task SymbolResponseForChangedListingIsDiscarded()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler((_, _) => pending.Task);
        await using var context = Context(handler);
        var listing = new AssetListingDto { Ticker = "VWCE", ExchangeMic = "XETR", TradingCurrency = "EUR" };
        var cut = context.Render<AdminSymbolDiscovery>(p => p.Add(x => x.Asset, new() { Name = "Fund", Type = AssetType.ETF })
            .Add(x => x.Listing, listing).Add(x => x.IsDraft, true).Add(x => x.AutoResolve, true));
        listing.Ticker = "CHANGED";
        pending.SetResult(Json(new[] { new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 1, "resolved", "VWCE.DEX") }));

        cut.WaitForAssertion(() => Assert.Contains("Listing details changed", cut.Markup));
        Assert.Empty(listing.MarketDataSymbols);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}