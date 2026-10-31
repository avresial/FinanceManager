using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Shared.Components.Dialogs;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Dividends;

[Trait("Category", "Unit")]
public class DividendPaymentHistoryTests
{
    private static readonly Currency _pln = new(0, "PLN", "zł");

    [Fact]
    public async Task EmptyHistory_ShowsEmptyMessageAndRecordButton()
    {
        var handler = new StubHttpHandler(request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal)
                ? JsonResponse(new[] { new DividendHoldingOption(42, "iShares Core S&P 500", "CSPX", "XLON", "USD") })
                : request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal)
                    ? JsonResponse(new DividendPaymentPage(Array.Empty<DividendPaymentDto>(), 0, 1, 20))
                    : request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal)
                        ? JsonResponse(new[] { _pln })
                        : JsonResponse(Array.Empty<DividendHoldingOption>())));

        await using var context = CreateBunitContext(handler);
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        Assert.Contains("No dividend payments yet.", cut.Markup);
        var recordButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Record dividend");
        Assert.False(recordButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task RenderedHistory_ShowsLatestFirstWithCashReceivedAndAccessibleActions()
    {
        var later = new DividendPaymentDto(101, 10, 42, "iShares Core S&P 500", "CSPX",
            DateOnly.FromDateTime(new DateTime(2026, 8, 1)), 120m, "USD", null);
        var earlier = new DividendPaymentDto(92, 10, 42, "iShares Core S&P 500", "CSPX",
            DateOnly.FromDateTime(new DateTime(2026, 5, 1)), 85m, "USD", "Quarterly.");

        var handler = new StubHttpHandler(request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal)
                ? JsonResponse(new[] { new DividendHoldingOption(42, "iShares Core S&P 500", "CSPX", "XLON", "USD") })
                : request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal)
                    ? JsonResponse(new DividendPaymentPage([later, earlier], 2, 1, 20))
                    : request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal)
                        ? JsonResponse(new[] { _pln })
                        : JsonResponse(Array.Empty<DividendHoldingOption>())));

        await using var context = CreateBunitContext(handler);
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        // Latest first: the August payment appears before the one in markup preceding it.
        Assert.True(cut.Markup.IndexOf("120.00 USD") < cut.Markup.IndexOf("85.00 USD"));
        var editButtons = cut.FindAll("button").Where(b => b.HasAttribute("aria-label") &&
            b.GetAttribute("aria-label")!.ToString().StartsWith("Edit dividend ", StringComparison.Ordinal)).ToList();
        var deleteButtons = cut.FindAll("button").Where(b => b.HasAttribute("aria-label") &&
            b.GetAttribute("aria-label")!.ToString().StartsWith("Delete dividend ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, editButtons.Count);
        Assert.Equal(2, deleteButtons.Count);
    }

    [Fact]
    public async Task RecordingDividend_SendsAddRequestAndAddsItToHistory()
    {
        DividendPaymentRequest? capturedRequest = null;
        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal))
            {
                capturedRequest = await JsonSerializer.DeserializeAsync<DividendPaymentRequest>(
                    await request.Content!.ReadAsStreamAsync(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return JsonResponse(701L);
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal))
            {
                var items = new List<DividendPaymentDto>();
                if (capturedRequest is not null)
                {
                    items.Add(new DividendPaymentDto(701, 10, capturedRequest.AssetListingId, "VWCE", "VWCE",
                        capturedRequest.PaymentDate, capturedRequest.Amount,
                        capturedRequest.Currency, capturedRequest.Notes));
                }
                return JsonResponse(new DividendPaymentPage(items, items.Count, 1, 20));
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal))
                return JsonResponse(new[] { new DividendHoldingOption(42, "Vanguard FTSE All-World", "VWCE", "XLON", "USD") });

            if (request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal))
                return JsonResponse(new[] { _pln, new Currency(1, "USD", "$") });

            return JsonResponse(Array.Empty<DividendHoldingOption>());
        });

        await using var context = CreateBunitContext(handler);
        var snackbar = context.Services.GetRequiredService<ISnackbar>();
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Record dividend").Click());

        // The payout currency is explicitly selectable independently of the listing.
        var holdingSelect = cut.FindComponents<MudSelect<long>>()
            .Single(s => s.Instance.Label == "Holding");
        await cut.InvokeAsync(() => holdingSelect.Instance.ValueChanged.InvokeAsync(42));

        var currencySelect = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Payout currency");
        await cut.InvokeAsync(() => currencySelect.Instance.ValueChanged.InvokeAsync("PLN"));

        var cashField = cut.FindComponents<MudNumericField<decimal>>()
            .Single(f => f.Instance.Label == "Cash received");
        await cut.InvokeAsync(() => cashField.Instance.ValueChanged.InvokeAsync(150m));

        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.False(saveButton.HasAttribute("disabled"));
        await cut.InvokeAsync(() => saveButton.Click());

        cut.WaitForAssertion(() => Assert.Contains("150.00 PLN", cut.Markup));
        Assert.DoesNotContain("role=\"dialog\"", cut.Markup);

        Assert.NotNull(capturedRequest);
        Assert.Equal(42, capturedRequest!.AssetListingId);
        Assert.Equal(150m, capturedRequest.Amount);
        Assert.Equal("PLN", capturedRequest.Currency);

        Assert.Contains(snackbar.ShownSnackbars, s => s.Message == "Dividend recorded.");
    }

    [Fact]
    public async Task ServerRejectedSave_KeepsErrorVisibleAndModalOpenWithoutReload()
    {
        var handler = new StubHttpHandler(request => Task.FromResult(
            request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal)
                ? BadResponse("The note exceeds the maximum length.")
                : request.Method == HttpMethod.Get &&
                    request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal)
                    ? JsonResponse(new DividendPaymentPage(Array.Empty<DividendPaymentDto>(), 0, 1, 20))
                    : request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal)
                        ? JsonResponse(new[] { new DividendHoldingOption(42, "Vanguard FTSE All-World", "VWCE", "XLON", "USD") })
                        : request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal)
                            ? JsonResponse(new[] { _pln })
                            : JsonResponse(Array.Empty<DividendHoldingOption>())));

        await using var context = CreateBunitContext(handler);
        var snackbar = context.Services.GetRequiredService<ISnackbar>();
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Record dividend").Click());

        var holdingSelect = cut.FindComponents<MudSelect<long>>()
            .Single(s => s.Instance.Label == "Holding");
        await cut.InvokeAsync(() => holdingSelect.Instance.ValueChanged.InvokeAsync(42));

        var cashField = cut.FindComponents<MudNumericField<decimal>>()
            .Single(f => f.Instance.Label == "Cash received");
        await cut.InvokeAsync(() => cashField.Instance.ValueChanged.InvokeAsync(100m));

        // Exercise a server rejection while preserving the entered form.
        var notesField = cut.FindComponent<MudTextField<string>>().Instance;
        await cut.InvokeAsync(() => notesField.ValueChanged.InvokeAsync("Rejected note"));
        await cut.InvokeAsync(() => cut.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(new DateTime(2026, 9, 1)));


        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.False(saveButton.HasAttribute("disabled"));
        await cut.InvokeAsync(() => saveButton.Click());

        cut.WaitForAssertion(() => Assert.Contains("The note exceeds the maximum length.", cut.Markup));
        Assert.Contains(snackbar.ShownSnackbars, s => s.Message == "The note exceeds the maximum length.");
        // A rejected save keeps the modal open and adds no new payment row to history.
        Assert.DoesNotContain("100.00 USD", cut.Markup);
        var cancelButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel");
        Assert.False(cancelButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task EditingDividend_SendsUpdateRequestAndReflectsNewValuesInHistory()
    {
        DividendPaymentRequest? capturedRequest = null;
        var original = new DividendPaymentDto(92, 10, 42, "VWCE", "VWCE",
            DateOnly.FromDateTime(new DateTime(2026, 5, 1)), 85m, "USD", null);

        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Put &&
                request.RequestUri!.AbsolutePath.Contains("/dividends/", StringComparison.Ordinal))
            {
                capturedRequest = await JsonSerializer.DeserializeAsync<DividendPaymentRequest>(
                    await request.Content!.ReadAsStreamAsync(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return new(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal))
            {
                var item = capturedRequest is not null
                    ? new DividendPaymentDto(92, 10, capturedRequest.AssetListingId, "VWCE", "VWCE",
                        capturedRequest.PaymentDate, capturedRequest.Amount,
                        capturedRequest.Currency, capturedRequest.Notes)
                    : original;
                return JsonResponse(new DividendPaymentPage([item], 1, 1, 20));
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal))
                return JsonResponse(Array.Empty<DividendHoldingOption>());

            if (request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal))
                return JsonResponse(new[] { _pln, new Currency(1, "USD", "$") });

            return JsonResponse(Array.Empty<DividendHoldingOption>());
        });

        await using var context = CreateBunitContext(handler);
        var snackbar = context.Services.GetRequiredService<ISnackbar>();
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        cut.WaitForAssertion(() => Assert.Contains("85.00 USD", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.HasAttribute("aria-label") &&
            b.GetAttribute("aria-label")!.ToString().StartsWith("Edit dividend ", StringComparison.Ordinal)).Click());

        var cashField = cut.FindComponents<MudNumericField<decimal>>()
            .Single(f => f.Instance.Label == "Cash received");
        await cut.InvokeAsync(() => cashField.Instance.ValueChanged.InvokeAsync(200m));

        var dateField = cut.FindComponent<MudDatePicker>().Instance;
        await cut.InvokeAsync(() => dateField.DateChanged.InvokeAsync(new DateTime(2026, 9, 1)));

        var saveButton = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.False(saveButton.HasAttribute("disabled"));
        await cut.InvokeAsync(() => saveButton.Click());

        cut.WaitForAssertion(() => Assert.Contains("200.00 USD", cut.Markup));

        Assert.NotNull(capturedRequest);
        Assert.Equal(42, capturedRequest!.AssetListingId);
        Assert.Equal(200m, capturedRequest.Amount);
        Assert.Equal(new DateTime(2026, 9, 1), capturedRequest.PaymentDate.ToDateTime(TimeOnly.MinValue));

        Assert.Contains(snackbar.ShownSnackbars, s => s.Message == "Dividend updated.");
    }

    [Fact]
    public async Task ConfirmingDelete_RemovesPaymentFromHistory()
    {
        var original = new DividendPaymentDto(92, 10, 42, "VWCE", "VWCE",
            DateOnly.FromDateTime(new DateTime(2026, 5, 1)), 85m, "USD", null);

        var deleted = false;
        var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.AbsolutePath.Contains("/dividends/", StringComparison.Ordinal))
            {
                deleted = true;
                return new(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal))
                return JsonResponse(new DividendPaymentPage(
                    deleted ? Array.Empty<DividendPaymentDto>() : [original], deleted ? 0 : 1, 1, 20));

            if (request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal))
                return JsonResponse(Array.Empty<DividendHoldingOption>());

            if (request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal))
                return JsonResponse(new[] { _pln, new Currency(1, "USD", "$") });

            return JsonResponse(Array.Empty<DividendHoldingOption>());
        });

        await using var context = CreateBunitContext(handler, confirmedDelete: true);
        var snackbar = context.Services.GetRequiredService<ISnackbar>();
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        cut.WaitForAssertion(() => Assert.Contains("85.00 USD", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.HasAttribute("aria-label") &&
            b.GetAttribute("aria-label")!.ToString().StartsWith("Delete dividend ", StringComparison.Ordinal)).Click());

        cut.WaitForAssertion(() => Assert.DoesNotContain("85.00 USD", cut.Markup));
        Assert.Contains(snackbar.ShownSnackbars, s => s.Message == "Dividend removed.");
    }

    [Fact]
    public async Task CancellingDelete_DoesNotRemovePayment()
    {
        var original = new DividendPaymentDto(92, 10, 42, "VWCE", "VWCE",
            DateOnly.FromDateTime(new DateTime(2026, 5, 1)), 85m, "USD", null);

        var handler = new StubHttpHandler(request => Task.FromResult(
            request.Method == HttpMethod.Delete &&
                request.RequestUri!.AbsolutePath.Contains("/dividends/", StringComparison.Ordinal)
                ? new(HttpStatusCode.NoContent)
                : request.Method == HttpMethod.Get &&
                    request.RequestUri!.AbsolutePath.EndsWith("/dividends", StringComparison.Ordinal)
                    ? JsonResponse(new DividendPaymentPage([original], 1, 1, 20))
                    : request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal)
                        ? JsonResponse(Array.Empty<DividendHoldingOption>())
                        : request.RequestUri!.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal)
                            ? JsonResponse(new[] { _pln })
                            : JsonResponse(Array.Empty<DividendHoldingOption>())));

        await using var context = CreateBunitContext(handler, confirmedDelete: false);
        var cut = context.Render<DividendPaymentHistory>(p => p
            .Add(x => x.AccountId, 10)
            .Add(x => x.DefaultCurrency, "PLN"));

        cut.WaitForAssertion(() => Assert.Contains("85.00 USD", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.HasAttribute("aria-label") &&
            b.GetAttribute("aria-label")!.ToString().StartsWith("Delete dividend ", StringComparison.Ordinal)).Click());

        cut.WaitForAssertion(() => Assert.Contains("85.00 USD", cut.Markup));
    }

    [Fact]
    public async Task LoadOlderPayments_RequestsNextPageAndAppendsRows()
    {
        var requestedPages = new List<string>();
        var handler = new StubHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/Currency/GetAll", StringComparison.Ordinal)) return Task.FromResult(JsonResponse(new[] { _pln }));
            if (path.EndsWith("/holdings", StringComparison.Ordinal)) return Task.FromResult(JsonResponse(Array.Empty<DividendHoldingOption>()));
            var second = request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal);
            requestedPages.Add(request.RequestUri.Query);
            return Task.FromResult(JsonResponse(new DividendPaymentPage(
                [new(second ? 1 : 2, 10, 42, second ? "Older fund" : "Latest fund", "FUND", new(2020, 1, 1), 5, "PLN", null)], 21, second ? 2 : 1, 20)));
        });
        await using var context = CreateBunitContext(handler);
        var cut = context.Render<DividendPaymentHistory>(p => p.Add(x => x.AccountId, 10));
        cut.WaitForAssertion(() => Assert.Contains("Latest fund", cut.Markup));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(x => x.TextContent.Trim() == "Load older payments").Click());
        cut.WaitForAssertion(() => Assert.Contains("Older fund", cut.Markup));
        Assert.Contains("Latest fund", cut.Markup);
        Assert.Equal(2, requestedPages.Count);
        Assert.Contains("page=2", requestedPages[1]);
        Assert.DoesNotContain("Load older payments", cut.Markup);
    }

    [Fact]
    public async Task FailedHoldingLoad_IsAnErrorRatherThanAnEmptyAccount()
    {
        var handler = new StubHttpHandler(request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/holdings", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : request.RequestUri.AbsolutePath.EndsWith("/Currency/GetAll", StringComparison.Ordinal)
                    ? JsonResponse(new[] { _pln })
                    : JsonResponse(new DividendPaymentPage([], 0, 1, 20))));
        await using var context = CreateBunitContext(handler);
        var cut = context.Render<DividendPaymentHistory>(p => p.Add(x => x.AccountId, 10));
        cut.WaitForAssertion(() => Assert.Contains("Could not load dividend payments", cut.Markup));
        Assert.DoesNotContain("No dividend payments yet", cut.Markup);
        Assert.Contains("Retry", cut.Markup);
    }

    [Fact]
    public async Task ChangingAccount_DiscardsAnOlderPendingHistoryResponse()
    {
        var oldResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/Currency/GetAll", StringComparison.Ordinal)) return Task.FromResult(JsonResponse(new[] { _pln }));
            if (path.EndsWith("/holdings", StringComparison.Ordinal)) return Task.FromResult(JsonResponse(Array.Empty<DividendHoldingOption>()));
            if (path.Contains("/10/", StringComparison.Ordinal)) return oldResponse.Task;
            return Task.FromResult(JsonResponse(new DividendPaymentPage([new(3, 11, 42, "Current account fund", "FUND", new(2020, 1, 1), 5, "PLN", null)], 1, 1, 20)));
        });
        await using var context = CreateBunitContext(handler);
        var cut = context.Render<DividendPaymentHistory>(p => p.Add(x => x.AccountId, 10));
        cut.Render(p => p.Add(x => x.AccountId, 11));
        cut.WaitForAssertion(() => Assert.Contains("Current account fund", cut.Markup));
        oldResponse.SetResult(JsonResponse(new DividendPaymentPage([new(1, 10, 42, "Old account fund", "FUND", new(2020, 1, 1), 5, "PLN", null)], 1, 1, 20)));
        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.WaitForAssertion(() => Assert.Contains("Current account fund", cut.Markup));
        Assert.DoesNotContain("Old account fund", cut.Markup);
    }

    private static BunitContext CreateBunitContext(HttpMessageHandler handler, bool confirmedDelete = true)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        context.Services.AddSingleton(new DividendPaymentHttpClient(httpClient));
        context.Services.AddSingleton(new CurrencyHttpClient(httpClient));

        var dialogServiceMock = new Mock<IDialogService>();
        var referenceMock = new Mock<IDialogReference>();
        if (confirmedDelete)
            referenceMock.Setup(r => r.Result).Returns(Task.FromResult<DialogResult?>(DialogResult.Ok(true)));
        else
            referenceMock.Setup(r => r.Result).Returns(Task.FromResult<DialogResult?>(null));
        dialogServiceMock.Setup(d => d.ShowAsync<ConfirmDeleteDialog>("Delete dividend", It.IsAny<DialogParameters>()))
            .Returns(Task.FromResult(referenceMock.Object));
        context.Services.AddSingleton<IDialogService>(dialogServiceMock.Object);

        context.Services.AddSingleton<ILogger<DividendPaymentHistory>>(NullLogger<DividendPaymentHistory>.Instance);
        return context;
    }

    private static HttpResponseMessage JsonResponse<T>(T payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage BadResponse(string detail) =>
        new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { detail }), System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}