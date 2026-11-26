using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Extensions;
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
            .Add(x => x.Visible, true));

        var searchFunc = cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>().Instance.SearchFunc
            ?? throw new InvalidOperationException("SearchFunc must be configured.");
        await cut.InvokeAsync(async () =>
        {
            var task = searchFunc("ZZZZNOTREAL999", CancellationToken.None);
            if (task is not null)
                _ = await task;
        });
        cut.FindComponent<InvestmentTransactionFormBody>().Render();

        Assert.Contains("No recognized instrument matched \"ZZZZNOTREAL999\"", cut.Markup);
        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.True(saveButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task IncompleteExternalInstrument_EnablesCurrencySelectorAndShowsUnpricedAdminNotificationAfterSave()
    {
        CreateInvestmentTradeRequest? capturedRequest = null;
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
            if (request.Method == HttpMethod.Post)
            {
                capturedRequest = await JsonSerializer.DeserializeAsync<CreateInvestmentTradeRequest>(
                    await request.Content!.ReadAsStreamAsync(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return JsonResponse(new InvestmentTransactionDto(
                    Id: 501,
                    UserId: 1,
                    AccountId: null,
                    AssetListingId: 77,
                    Type: InvestmentTransactionTypeDto.Buy,
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

        Assert.Equal("Xetra (XETR) · EUR · Existing", InvestmentTransactionFormBody.FormatInstrumentSubtitle(complete));
        Assert.Equal("Unknown venue · Unknown currency · External", InvestmentTransactionFormBody.FormatInstrumentSubtitle(incomplete));
    }

    [Fact]
    public async Task SelectedLabel_ShowsNameInsteadOfId()
    {
        await using var context = CreateBunitContext(new StubHttpHandler(_ => Task.FromResult(JsonResponse(Array.Empty<Currency>()))),
            [new FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel { Id = 7, Name = "Long term" }]);
        context.Render<MudPopoverProvider>();
        var cut = context.Render<InvestmentTransactionForm>(p => p.Add(x => x.Visible, true));
        var select = cut.FindComponent<MudSelect<int>>();
        await cut.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(new int[] { 7 }));
        cut.WaitForAssertion(() => Assert.Equal("Long term", select.Instance.GetState(x => x.Text)));
    }

    [Fact]
    public async Task ParentRerenderWhileEditInitializes_DoesNotStartASecondInitialization()
    {
        var labelsReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var labelRequests = 0;
        var labelHandler = new StubHttpHandler(async _ =>
        {
            Interlocked.Increment(ref labelRequests);
            await labelsReleased.Task;
            return JsonResponse(Array.Empty<FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel>());
        });
        await using var context = CreateBunitContext(new StubHttpHandler(_ => Task.FromResult(JsonResponse(Array.Empty<Currency>()))),
            labelHandler: labelHandler);
        var trade = new InvestmentTransactionDto(5, 1, 10, 1, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN",
            DateOnly.FromDateTime(DateTime.Today), null, null, "TCK", Labels: []);
        var cut = context.Render<InvestmentTransactionForm>(p => p.Add(x => x.Visible, true).Add(x => x.Transaction, trade));
        cut.WaitForAssertion(() => Assert.Equal(1, Volatile.Read(ref labelRequests)));

        // The Transactions page re-renders when valuations arrive, handing the form the same trade again.
        cut.Render(p => p.Add(x => x.Visible, true).Add(x => x.Transaction, trade));
        labelsReleased.SetResult();
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref labelRequests));
    }

    [Fact]
    public async Task EditForm_KeepsSaveDisabled_UntilTheTradeIsLoadedIntoTheForm()
    {
        var labelsReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var labelHandler = new StubHttpHandler(async _ =>
        {
            await labelsReleased.Task;
            return JsonResponse(Array.Empty<FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel>());
        });
        await using var context = CreateBunitContext(new StubHttpHandler(_ => Task.FromResult(JsonResponse(Array.Empty<Currency>()))),
            labelHandler: labelHandler);
        var trade = new InvestmentTransactionDto(5, 1, 10, 1, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN",
            DateOnly.FromDateTime(DateTime.Today), null, null, "TCK", Labels: []);

        var cut = context.Render<InvestmentTransactionForm>(p => p.Add(x => x.Visible, true).Add(x => x.Transaction, trade));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save"));

        // The user picks an instrument while the trade is still loading; saving now would add a new trade
        // instead of updating this one.
        var picker = cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>();
        await cut.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(new InvestmentInstrumentOptionDto
        {
            ResultId = "ext-1",
            Source = InstrumentOptionSource.External,
            Ticker = "VWCE",
            DisplayName = "VWCE",
            TradingCurrency = "USD"
        }));
        Assert.True(save().Instance.Disabled);
        labelsReleased.SetResult();
        cut.WaitForAssertion(() => Assert.False(save().Instance.Disabled));
    }

    [Fact]
    public async Task NewTradeIsCreatedThroughTheOwnerRoute()
    {
        CreateInvestmentTradeRequest? captured = null;
        string? path = null;
        var option = new InvestmentInstrumentOptionDto
        {
            ResultId = "listing:1",
            Source = InstrumentOptionSource.Existing,
            AssetListingId = 1,
            Ticker = "CSPX",
            DisplayName = "iShares Core S&P 500",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        };
        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                path = request.RequestUri!.AbsolutePath;
                captured = await JsonSerializer.DeserializeAsync<CreateInvestmentTradeRequest>(
                    await request.Content!.ReadAsStreamAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new InvestmentTransactionDto(
                        9, 1, null, 1, InvestmentTransactionTypeDto.Buy, 1m, 5m, "USD", captured!.TradeDate, null, null, "CSPX")),
                        System.Text.Encoding.UTF8, "application/json")
                };
            }

            return request.RequestUri!.AbsolutePath.Contains("/ListingPrice/") ? JsonResponse(new ListingPriceDto(5m, "USD")) : JsonResponse(Array.Empty<Currency>());
        });
        await using var context = CreateBunitContext(handler);
        context.Render<MudPopoverProvider>();
        var saved = 0;
        var cut = context.Render<InvestmentTransactionForm>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.OnSaved, () => saved++));

        await cut.InvokeAsync(() => cut.FindComponent<MudAutocomplete<InvestmentInstrumentOptionDto>>().Instance.SelectOptionAsync(option));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").HasAttribute("disabled")));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click());

        Assert.Equal("/api/transactions/investment", path);
        Assert.Equal((1L, "USD", 5m), (captured!.AssetListingId, captured.Currency, captured.UnitPrice));
        Assert.Equal(1, saved);
    }

    [Fact]
    public async Task EditedTradeIsUpdatedThroughTheOwnerRoute()
    {
        UpdateInvestmentTradeRequest? captured = null;
        string? path = null;
        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                path = request.RequestUri!.AbsolutePath;
                captured = await JsonSerializer.DeserializeAsync<UpdateInvestmentTradeRequest>(
                    await request.Content!.ReadAsStreamAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return JsonResponse(Array.Empty<Currency>());
        });
        await using var context = CreateBunitContext(handler);
        var trade = new InvestmentTransactionDto(5, 1, null, 3, InvestmentTransactionTypeDto.Buy, 2m, 10m, "PLN",
            DateOnly.FromDateTime(DateTime.Today), null, null, "TCK", Labels: []);
        var cut = context.Render<InvestmentTransactionForm>(p => p.Add(x => x.Visible, true).Add(x => x.Transaction, trade));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click());

        Assert.Equal("/api/transactions/investment/5", path);
        Assert.Equal((3L, 2m, 10m), (captured!.AssetListingId, captured.Quantity, captured.UnitPrice));
    }

    private static BunitContext CreateBunitContext(HttpMessageHandler handler, FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel[]? labels = null,
        HttpMessageHandler? labelHandler = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        context.Services.AddSingleton(new InvestmentTransactionHttpClient(httpClient));
        context.Services.AddSingleton(new CurrencyHttpClient(httpClient));
        context.Services.AddSingleton(new InvestmentTradesHttpClient(httpClient));
        context.Services.AddSingleton(new FinanceManager.Components.Features.Labels.HttpClients.FinancialLabelHttpClient(new HttpClient(labelHandler ?? new StubHttpHandler(_ => Task.FromResult(JsonResponse(labels ?? [])))) { BaseAddress = new Uri("https://localhost/") }));
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(x => x.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settingsService.Object);
        context.Services.AddSingleton<ILogger<InvestmentTransactionFormBody>>(NullLogger<InvestmentTransactionFormBody>.Instance);
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