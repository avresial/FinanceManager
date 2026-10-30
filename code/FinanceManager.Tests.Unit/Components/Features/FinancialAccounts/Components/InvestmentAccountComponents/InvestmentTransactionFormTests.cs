using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

[Trait("Category", "Unit")]
public class InvestmentTransactionFormTests
{
    [Fact]
    public async Task UnrecognizedSearchInput_ShowsWarningBeforeSaveAndKeepsSaveDisabled()
    {
        await using var context = CreateBunitContext(new StubHttpHandler(_ => Task.FromResult(JsonResponse(Array.Empty<InvestmentInstrumentOptionDto>()))));
        context.Render<MudPopoverProvider>();
        var cut = context.Render<InvestmentTransactionForm>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.Visible, true));

        var searchFunc = cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>().Instance.SearchFunc
            ?? throw new InvalidOperationException("SearchFunc must be configured.");
        await cut.InvokeAsync(async () =>
        {
            var task = searchFunc("ZZZZNOTREAL999", CancellationToken.None);
            if (task is not null)
                _ = await task;
        });
        cut.Render();

        Assert.Contains("No recognized instrument matched \"ZZZZNOTREAL999\"", cut.Markup);
        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.True(saveButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task IncompleteExternalInstrument_EnablesCurrencySelectorAndShowsUnpricedAdminNotificationAfterSave()
    {
        AddInvestmentTransactionRequest? capturedRequest = null;
        var externalOption = new InvestmentInstrumentOptionDto
        {
            ResultId = "ext-incomplete-1",
            Source = InstrumentOptionSource.External,
            Ticker = "VWCE",
            DisplayName = "VANG FTSE AW USDA",
            ExchangeMic = null,
            ExchangeName = "GF",
            TradingCurrency = string.Empty,
            Warnings =
            [
                "Exchange venue could not be mapped automatically; an administrator can complete it later.",
                "Trading currency was not provided; select the currency for this transaction."
            ]
        };

        var handler = new StubHttpHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/Add", StringComparison.Ordinal))
            {
                capturedRequest = await JsonSerializer.DeserializeAsync<AddInvestmentTransactionRequest>(
                    await request.Content!.ReadAsStreamAsync(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return JsonResponse(new InvestmentTransactionDto(
                    Id: 501,
                    UserId: 1,
                    AccountId: 10,
                    AssetListingId: 77,
                    Type: InvestmentTransactionType.Buy,
                    Quantity: 2m,
                    UnitPrice: 118.25m,
                    Currency: capturedRequest!.Currency,
                    TradeDate: capturedRequest.TradeDate,
                    Fee: null,
                    Notes: null,
                    Ticker: "VWCE",
                    ExchangeName: "GF",
                    AutomaticPricesAvailable: false));
            }

            return JsonResponse(new[] { externalOption });
        });

        await using var context = CreateBunitContext(handler);
        var snackbar = context.Services.GetRequiredService<ISnackbar>();
        context.Render<MudPopoverProvider>();
        var cut = context.Render<InvestmentTransactionForm>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.Visible, true));

        var autocomplete = cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>();
        await cut.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(externalOption));

        // Currency must be selectable via MudSelect when TradingCurrency is unknown
        var currencySelect = cut.FindComponents<MudSelect<string>>()
            .Single(s => s.Instance.Label == "Currency");
        await cut.InvokeAsync(() => currencySelect.Instance.SelectOption("EUR"));

        // Enter manual unit price
        var unitPriceField = cut.FindComponents<MudNumericField<decimal>>()
            .Single(f => f.Instance.Label == "Unit price");
        Assert.False(unitPriceField.Instance.Disabled);
        await cut.InvokeAsync(() => unitPriceField.Instance.ValueChanged.InvokeAsync(118.25m));

        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.False(saveButton.HasAttribute("disabled"));
        await cut.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(capturedRequest);
        Assert.Equal("ext-incomplete-1", capturedRequest!.ExternalInstrumentResultId);
        Assert.Null(capturedRequest.AssetListingId);
        Assert.Equal("EUR", capturedRequest.Currency);
        Assert.Equal(118.25m, capturedRequest.UnitPrice);

        Assert.Contains(
            snackbar.ShownSnackbars,
            s => s.Message == "Added. Automatic prices aren't available yet; an administrator has been notified.");
    }

    [Fact]
    public async Task SelectingListingWithoutCurrency_AfterPricedListing_ResetsUnitPrice()
    {
        var priced = new InvestmentInstrumentOptionDto
        {
            ResultId = "listing:1",
            Source = InstrumentOptionSource.Existing,
            AssetListingId = 1,
            Ticker = "CSPX",
            DisplayName = "iShares Core S&P 500",
            ExchangeName = "London Stock Exchange",
            ExchangeMic = "XLON",
            TradingCurrency = "USD"
        };
        var withoutCurrency = new InvestmentInstrumentOptionDto
        {
            ResultId = "listing:2",
            Source = InstrumentOptionSource.Existing,
            AssetListingId = 2,
            Ticker = "VWRA",
            DisplayName = "Vanguard FTSE All-World",
            ExchangeName = "London Stock Exchange",
            ExchangeMic = "XLON",
            TradingCurrency = string.Empty
        };

        var handler = new StubHttpHandler(request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/ListingPrice/1", StringComparison.Ordinal)
                ? JsonResponse(new ListingPriceDto(321.45m, "USD"))
                : JsonResponse(Array.Empty<InvestmentInstrumentOptionDto>())));

        await using var context = CreateBunitContext(handler);
        context.Render<MudPopoverProvider>();
        var cut = context.Render<InvestmentTransactionForm>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.Visible, true));
        var autocomplete = cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>();
        // The field formats with the current culture, so parse it back the same way.
        decimal UnitPrice() => decimal.Parse(
            cut.FindComponents<MudNumericField<decimal>>()
                .Single(f => f.Instance.Label == "Unit price")
                .Find("input").GetAttribute("value")!,
            System.Globalization.CultureInfo.CurrentCulture);

        await cut.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(priced));
        cut.WaitForAssertion(() => Assert.Equal(321.45m, UnitPrice()));

        await cut.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(withoutCurrency));
        cut.WaitForAssertion(() => Assert.Equal(0m, UnitPrice()));
    }

    [Fact]
    public void FormatInstrumentSubtitle_HandlesCompleteAndIncompleteVenuesAndCurrencies()
    {
        var complete = new InvestmentInstrumentOptionDto
        {
            Source = InstrumentOptionSource.Existing,
            ExchangeName = "Xetra",
            ExchangeMic = "XETR",
            TradingCurrency = "EUR"
        };
        var incomplete = new InvestmentInstrumentOptionDto
        {
            Source = InstrumentOptionSource.External,
            ExchangeName = string.Empty,
            ExchangeMic = null,
            TradingCurrency = string.Empty
        };

        Assert.Equal("Xetra (XETR) · EUR · Existing", InvestmentTransactionForm.FormatInstrumentSubtitle(complete));
        Assert.Equal("Unknown venue · Unknown currency · External", InvestmentTransactionForm.FormatInstrumentSubtitle(incomplete));
    }

    private static BunitContext CreateBunitContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        context.Services.AddSingleton(new InvestmentTransactionHttpClient(httpClient));
        context.Services.AddSingleton(new CurrencyHttpClient(httpClient));
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(x => x.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settingsService.Object);
        context.Services.AddSingleton<ILogger<InvestmentTransactionForm>>(NullLogger<InvestmentTransactionForm>.Instance);
        return context;
    }

    private static HttpResponseMessage JsonResponse<T>(T payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}