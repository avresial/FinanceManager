using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Components;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Components;

[Trait("Category", "Unit")]
public class TransactionsExportDialogTests
{
    private static readonly DateTime _start = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartsOnThePassedCurrencyAndRange()
    {
        await using var context = CreateContext(new HttpResponseMessage(HttpStatusCode.OK));

        var (cut, _) = await Open(context, TransactionKind.Cash, "USD");

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("input"), input => input.GetAttribute("value") == DefaultCurrency.USD.ShortName));
        var pickers = cut.FindComponents<MudDatePicker>();
        Assert.Equal([_start.Date, _end.Date], pickers.Select(picker => picker.Instance.Date));
    }

    [Fact]
    public async Task CashExport_RequestsTheLocalDaysAsUtc_AndDownloadsTheFile()
    {
        var csv = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("a;b"u8.ToArray()) };
        csv.Content.Headers.ContentDisposition = new("attachment") { FileName = "cash-usd.csv" };
        var handler = CreateHandler(csv);
        await using var context = CreateContext(handler);
        var download = context.JSInterop.SetupVoid("financeManager.downloadFileFromBase64", _ => true);
        var (cut, _) = await Open(context, TransactionKind.Cash, "USD");
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=export-submit]").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.Find("[data-testid=export-submit]").Click());

        var (startUtc, endUtc) = DateRangeHelper.LocalDaysToUtc(_start, _end);
        var request = Assert.Single(handler.Requests, r => r.PathAndQuery.StartsWith("/api/transactions/cash/export", StringComparison.Ordinal));
        Assert.Contains($"currencyId={DefaultCurrency.USD.Id}", request.PathAndQuery);
        Assert.Contains(Uri.EscapeDataString(startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture)), request.PathAndQuery);
        Assert.Contains(Uri.EscapeDataString(endUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture)), request.PathAndQuery);
        // Named from the days the user picked, not from the UTC bounds the server used.
        Assert.Equal("cash-usd-20260301-20260315.csv", Assert.Single(download.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task BondExport_RequestsTheBondRoute_AndNamesTheFileFromThePickedDays()
    {
        var csv = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("a;b"u8.ToArray()) };
        var handler = CreateHandler(csv, [Bond(5)]);
        await using var context = CreateContext(handler);
        var download = context.JSInterop.SetupVoid("financeManager.downloadFileFromBase64", _ => true);
        var (cut, _) = await Open(context, TransactionKind.Bond, null);
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=export-submit]").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.Find("[data-testid=export-submit]").Click());

        var request = Assert.Single(handler.Requests, r => r.PathAndQuery.StartsWith("/api/transactions/bonds/export", StringComparison.Ordinal));
        Assert.Contains("bondDetailsId=5", request.PathAndQuery);
        Assert.Equal("bond-5-20260301-20260315.csv", Assert.Single(download.Invocations).Arguments[0]);
    }

    [Fact]
    public async Task ANonHttpFailure_ShowsAGenericMessage_NotTheExceptionText()
    {
        var csv = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("a;b"u8.ToArray()) };
        await using var context = CreateContext(csv);
        context.JSInterop.SetupVoid("financeManager.downloadFileFromBase64", _ => true).SetException(new InvalidOperationException("js interop internals"));
        var (cut, _) = await Open(context, TransactionKind.Cash, null);
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=export-submit]").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.Find("[data-testid=export-submit]").Click());

        Assert.Equal("Could not export the transactions.", cut.Find("[data-testid=export-message]").TextContent);
    }

    [Fact]
    public async Task AnEmptyFile_ShowsNothingToExport_AndDownloadsNothing()
    {
        var handler = CreateHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });
        await using var context = CreateContext(handler);
        var (cut, _) = await Open(context, TransactionKind.Cash, null);
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=export-submit]").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.Find("[data-testid=export-submit]").Click());

        Assert.Contains("Nothing to export", cut.Find("[data-testid=export-message]").TextContent);
        Assert.DoesNotContain(context.JSInterop.Invocations, call => call.Identifier == "financeManager.downloadFileFromBase64");
    }

    [Fact]
    public async Task AFailedExport_ShowsTheServersMessage_AndKeepsTheDialogOpen()
    {
        var handler = CreateHandler(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("\"Start must be on or before end.\"") });
        await using var context = CreateContext(handler);
        var (cut, dialog) = await Open(context, TransactionKind.Cash, null);
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=export-submit]").HasAttribute("disabled")));

        await cut.InvokeAsync(() => cut.Find("[data-testid=export-submit]").Click());

        Assert.Equal("Start must be on or before end.", cut.Find("[data-testid=export-message]").TextContent);
        Assert.False(dialog.Result.IsCompleted);
    }

    private static async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Dialog)> Open(BunitContext context, TransactionKind kind, string? currency)
    {
        var provider = context.Render<MudDialogProvider>();
        var dialog = await context.Services.GetRequiredService<IDialogService>().ShowAsync<TransactionsExportDialog>("Export CSV", new DialogParameters<TransactionsExportDialog>
        {
            { x => x.Kind, kind },
            { x => x.CurrencyCode, currency },
            { x => x.RangeStart, _start },
            { x => x.RangeEnd, _end },
        });
        provider.WaitForElement("[data-testid=export-submit]");
        return (provider, dialog);
    }

    private static BondDetails Bond(int id) => new($"Bond {id}", "Issuer", new DateOnly(2023, 1, 1), new DateOnly(2030, 1, 1),
        [new BondCalculationMethod { Id = id, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 0.05m }])
    { Id = id };

    private static RecordingHttpHandler CreateHandler(HttpResponseMessage export, IReadOnlyList<BondDetails>? bonds = null) => new(request =>
        request.RequestUri!.AbsolutePath switch
        {
            "/api/Currency/GetAll" => new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(new List<Currency> { DefaultCurrency.PLN, DefaultCurrency.USD }) },
            "/api/BondDetails" => new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(bonds ?? []) },
            _ => export
        });

    private static BunitContext CreateContext(HttpResponseMessage export) => CreateContext(CreateHandler(export));

    private static BunitContext CreateContext(RecordingHttpHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = handler.CreateClient();
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return context;
    }
}