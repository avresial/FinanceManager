using Bunit;
using FinanceManager.Components.Features.Administration.Components;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration;

[Trait("Category", "Unit")]
public class IncompleteAssetRepairTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SourceReview_DoesNotWriteUntilSave_AndPreservesCuratedValues(int conflict)
    {
        var writes = 0;
        AssetDto? savedAsset = null;
        AssetListingDto? savedListing = null;
        using var handler = new Handler(async (request, ct) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/Currency/GetAll", StringComparison.Ordinal)) return Json(new[] { new Currency(1, "PLN", "zł") });
            if (path.EndsWith("/discovery/search", StringComparison.Ordinal)) return Json(new[] { new InstrumentDiscoveryResultDto
            {
                DisplayName = "External name", Ticker = "TEST", Isin = "CURATED", ShareClassFigi = "FIGI", ExchangeMic = "XWAR", TradingCurrency = "PLN", SecurityType = "ETF", CompositeFigi = conflict == 1 ? "CONFLICT" : null, ListingFigi = conflict == 2 ? "CONFLICT" : null
            } });
            if (path.EndsWith("/discovery/draft", StringComparison.Ordinal)) return Json(new AdminAssetDraftDto { Asset = new() { Type = AssetType.ETF, Isin = "CURATED", ShareClassFigi = "FIGI" } });
            if (request.Method == HttpMethod.Get) return Json(new AssetDto
            {
                Id = 1,
                Name = "Curated name",
                Isin = "CURATED",
                Type = AssetType.Stock,
                CompositeFigi = "CURATEDCOMPOSITE",
                Listings = [new() { Id = 4, AssetId = 1, Ticker = "TEST", ExchangeMic = "", TradingCurrency = "", ListingFigi = "CURATEDLISTING" }]
            });
            writes++;
            if (path.EndsWith("/listings/4", StringComparison.Ordinal)) savedListing = await request.Content!.ReadFromJsonAsync<AssetListingDto>(ct);
            else savedAsset = await request.Content!.ReadFromJsonAsync<AssetDto>(ct);
            return new(HttpStatusCode.NoContent);
        });
        await using var context = Context(handler);
        var cut = context.Render<AdminIncompleteAssetRepair>(p => p.Add(x => x.Item, Item()));
        cut.WaitForAssertion(() => Assert.Contains("Complete from sources", cut.Markup));
        await Button(cut, "Complete from sources").ClickAsync(new MouseEventArgs());
        Assert.Equal(0, writes);
        if (conflict is 1 or 2)
        {
            Assert.Contains("No source matches found", cut.Markup);
            Assert.DoesNotContain("Review missing fields</span>", cut.Markup);
            return;
        }
        if (conflict == 3)
        {
            cut.FindComponent<AdminAssetListingFields>().Instance.Listing.ExchangeMic = "XLON";
            await Button(cut, "Review missing fields").ClickAsync(new MouseEventArgs());
            Assert.Contains("Listing details changed", cut.Markup);
            Assert.Equal(0, writes);
            return;
        }
        await Button(cut, "Review missing fields").ClickAsync(new MouseEventArgs());
        Assert.Equal(0, writes);
        await Button(cut, "Save reviewed fields").ClickAsync(new MouseEventArgs());
        Assert.Equal("Curated name", savedAsset!.Name);
        Assert.Equal("CURATED", savedAsset.Isin);
        Assert.Equal(AssetType.Stock, savedAsset.Type);
        Assert.Equal("FIGI", savedAsset.ShareClassFigi);
        Assert.Equal("XWAR", savedListing!.ExchangeMic);
        Assert.Equal("PLN", savedListing.TradingCurrency);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<AdminIncompleteAssetRepair> cut, string text) =>
        cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
    private static IncompleteAssetItem Item() => new(1, "Curated name", 4, "TEST", null, null, 2, null, true, true, false, false, true, false, false);
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static BunitContext Context(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        context.Services.AddSingleton(new AssetHttpClient(http, NullLogger<AssetHttpClient>.Instance));
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Render<MudPopoverProvider>();
        return context;
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}