using Bunit;
using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.Components.Crud;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.Components.Crud;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Forms;

/// <summary>The cash and bond entry forms without an account: they save through the owner routes.</summary>
[Trait("Category", "Unit")]
public class OwnerEntryFormTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private static readonly FinancialLabel _food = new() { Id = 5, Name = "Food" };
    private static readonly BondDetails _bond = new() { Id = 3, Name = "EDO1234", Type = default, Currency = DefaultCurrency.PLN };

    [Fact]
    public async Task AddCash_PostsTheSelectedCurrency_TheLabelsAndTheEnteredDate()
    {
        var handler = RecordingHttpHandler.ForEntryForms([_food], saved: CashItem());
        await using var context = CreateContext(handler);
        var cut = context.Render<AddCurrencyEntry>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<Currency>>().Instance.ValueChanged.InvokeAsync(DefaultCurrency.USD));
        await cut.InvokeAsync(() => cut.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(TimeSpan.Zero));
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<string>>().Instance.SelectOption("Food"));
        cut.Instance.BalanceChange = -12.5m;
        cut.Instance.Description = "Lunch";
        var completed = 0;
        cut.Render(p => p.Add(x => x.ActionCompleted, () => completed++));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Add").Click());

        cut.WaitForAssertion(() => Assert.Equal(1, completed));
        var request = Assert.Single(handler.Requests, r => r.PathAndQuery == "/api/transactions/cash");
        var body = JsonSerializer.Deserialize<CreateCashEntryRequest>(request.Body, _web)!;
        Assert.Equal((DefaultCurrency.USD.Id, -12.5m, "Lunch"), (body.CurrencyId, body.ValueChange, body.Description));
        Assert.Equal([5], body.LabelIds);
        Assert.Equal(DateTime.Today, body.PostingDate);
    }

    [Fact]
    public async Task EditCash_PutsTheChangesToTheOwnerRoute_KeepingTheEntrysCurrency()
    {
        var handler = RecordingHttpHandler.ForEntryForms([_food], saved: CashItem());
        await using var context = CreateContext(handler);
        var entry = new CashEntry(42, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), -3m)
        {
            Description = "Old",
            CurrencyId = DefaultCurrency.USD.Id,
        };
        var cut = context.Render<UpdateCurrencyEntry>(p => p.Add(x => x.CashEntry, entry));
        cut.WaitForAssertion(() => Assert.Contains("Edit cash entry", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains(DefaultCurrency.USD.ShortName, cut.Find(".mud-typography-subtitle1").TextContent));
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().Single(t => t.Instance.Label == "Description").Instance.ValueChanged.InvokeAsync("New"));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Update").Click());

        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal("/api/transactions/cash/42", request.PathAndQuery);
        var body = JsonSerializer.Deserialize<UpdateCashEntryRequest>(request.Body, _web)!;
        Assert.Equal((-3m, "New"), (body.ValueChange, body.Description));
        Assert.Equal(new DateTime(2026, 1, 2), body.PostingDate.Date);
    }

    [Fact]
    public async Task EditCash_ShowsTheLocalTimeTheRowShows()
    {
        await using var context = CreateContext(RecordingHttpHandler.ForEntryForms([_food], saved: CashItem()));
        var entry = new CashEntry(42, new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc), -3m) { Description = "Old", CurrencyId = DefaultCurrency.USD.Id };

        var cut = context.Render<UpdateCurrencyEntry>(p => p.Add(x => x.CashEntry, entry));

        // Rows show PostingDate.ToLocalTime(); an edit form that showed the raw UTC hour would disagree with the row it came from.
        var shown = entry.PostingDate.ToLocalTime().ToString("HH:mm");
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("input"), input => input.GetAttribute("value") == shown));
    }

    [Fact]
    public async Task AddBond_PostsTheSelectedBond()
    {
        var handler = RecordingHttpHandler.ForEntryForms([_food], [_bond], saved: BondItem());
        await using var context = CreateContext(handler);
        var cut = context.Render<AddBondEntry>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<BondDetails>>().Instance.ValueChanged.InvokeAsync(_bond));
        await cut.InvokeAsync(() => cut.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(TimeSpan.Zero));
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<string>>().Instance.SelectOption("Food"));
        cut.Instance.ValueChange = 4m;
        cut.Render();

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Add").Click());

        var request = Assert.Single(handler.Requests, r => r.PathAndQuery == "/api/transactions/bonds");
        var body = JsonSerializer.Deserialize<SaveBondEntryRequest>(request.Body, _web)!;
        Assert.Equal((3, 4m), (body.BondDetailsId, body.ValueChange));
        Assert.Equal([5], body.LabelIds);
    }

    [Fact]
    public async Task EditBond_PutsTheChangesToTheOwnerRoute()
    {
        var handler = RecordingHttpHandler.ForEntryForms([], [_bond], saved: BondItem());
        await using var context = CreateContext(handler);
        var entry = new BondEntry(9, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 2m, _bond.Id) { Description = "Old" };
        var cut = context.Render<UpdateBondEntry>(p => p.Add(x => x.BondEntry, entry));
        cut.WaitForAssertion(() => Assert.Contains("Edit bond entry", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("EDO1234", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Update").Click());

        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal("/api/transactions/bonds/9", request.PathAndQuery);
        var body = JsonSerializer.Deserialize<SaveBondEntryRequest>(request.Body, _web)!;
        Assert.Equal((3, 2m), (body.BondDetailsId, body.ValueChange));
    }

    private static CurrencyTransactionLogItemDto CashItem() => new(1, DateTime.UtcNow, -1m, "x", 0, "PLN", null, []);

    private static BondTransactionLogItemDto BondItem() => new(1, DateTime.UtcNow, 1m, "x", 3, "PLN", "EDO1234", []);

    private static BunitContext CreateContext(RecordingHttpHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = handler.CreateClient();
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Services.AddSingleton(new FinancialLabelHttpClient(http));
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        context.Services.AddSingleton<TransactionDataSynchronizationService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return context;
    }
}