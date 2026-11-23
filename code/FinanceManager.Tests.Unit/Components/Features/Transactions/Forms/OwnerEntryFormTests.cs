using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Crud;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Crud;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
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
        Mock.Get(context.Services.GetRequiredService<IFinancialAccountService>()).VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddCash_WithAnAccount_StillSavesThroughTheAccountService()
    {
        var handler = RecordingHttpHandler.ForEntryForms();
        await using var context = CreateContext(handler);
        var cut = context.Render<AddCurrencyEntry>(p => p.Add(x => x.CurrencyAccount, new CurrencyAccount(1, 1, "Cash")));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input")));
        await cut.InvokeAsync(() => cut.FindComponent<MudTimePicker>().Instance.TimeChanged.InvokeAsync(TimeSpan.Zero));
        cut.Instance.BalanceChange = -1m;
        cut.Instance.Description = "x";
        cut.Render();

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Add").Click());

        Mock.Get(context.Services.GetRequiredService<IFinancialAccountService>())
            .Verify(service => service.AddEntry(It.IsAny<CurrencyAccountEntry>()), Times.Once);
        Assert.DoesNotContain(handler.Requests, r => r.PathAndQuery.StartsWith("/api/transactions/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EditCash_PutsTheChangesToTheOwnerRoute_KeepingTheEntrysCurrency()
    {
        var handler = RecordingHttpHandler.ForEntryForms([_food], saved: CashItem());
        await using var context = CreateContext(handler);
        var entry = new CurrencyAccountEntry(null, 42, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 0m, -3m)
        {
            Description = "Old",
            CurrencyId = DefaultCurrency.USD.Id,
        };
        var cut = context.Render<UpdateCurrencyEntry>(p => p.Add(x => x.CurrencyAccountEntry, entry));
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
        var entry = new CurrencyAccountEntry(null, 42, new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc), 0m, -3m) { Description = "Old", CurrencyId = DefaultCurrency.USD.Id };

        var cut = context.Render<UpdateCurrencyEntry>(p => p.Add(x => x.CurrencyAccountEntry, entry));

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
        var entry = new BondAccountEntry(null, 9, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 0m, 2m, _bond.Id) { Description = "Old" };
        var cut = context.Render<UpdateBondEntry>(p => p.Add(x => x.BondAccountEntry, entry));
        cut.WaitForAssertion(() => Assert.Contains("Edit bond entry", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("EDO1234", cut.Markup));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Update").Click());

        var request = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal("/api/transactions/bonds/9", request.PathAndQuery);
        var body = JsonSerializer.Deserialize<SaveBondEntryRequest>(request.Body, _web)!;
        Assert.Equal((3, 2m), (body.BondDetailsId, body.ValueChange));
    }

    private static CurrencyTransactionLogItemDto CashItem() => new(null, 1, DateTime.UtcNow, -1m, "x", 0, "PLN", null, []);

    private static BondTransactionLogItemDto BondItem() => new(null, 1, DateTime.UtcNow, 1m, "x", 3, "PLN", "EDO1234", []);

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
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton<AccountDataSynchronizationService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return context;
    }
}