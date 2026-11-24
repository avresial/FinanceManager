using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Crud;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Crud;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Components;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Components;

[Trait("Category", "Unit")]
public class AddTransactionDialogTests
{
    [Fact]
    public async Task OpensOnTheInitialKind_AndTheToggleSwapsTheForm()
    {
        await using var context = CreateContext();
        var cut = context.Render<AddTransactionDialog>(p => p.Add(x => x.Visible, true).Add(x => x.InitialKind, TransactionKind.Bond));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<AddBondEntry>()));
        Assert.Empty(cut.FindComponents<AddCurrencyEntry>());

        await Select(cut, "Cash");
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<AddCurrencyEntry>()));
        Assert.Empty(cut.FindComponents<AddBondEntry>());

        await Select(cut, "Investment");
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionFormBody>()));
        Assert.Empty(cut.FindComponents<AddCurrencyEntry>());
    }

    [Fact]
    public async Task ChangingTheKind_DiscardsWhatWasTyped()
    {
        await using var context = CreateContext();
        var cut = context.Render<AddTransactionDialog>(p => p.Add(x => x.Visible, true));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<AddCurrencyEntry>()));
        cut.FindComponent<AddCurrencyEntry>().Instance.Description = "half typed";

        await Select(cut, "Bond");
        await Select(cut, "Cash");

        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.FindComponent<AddCurrencyEntry>().Instance.Description));
    }

    [Fact]
    public async Task InvestmentForm_RendersWithoutANestedOverlay()
    {
        await using var context = CreateContext();
        var cut = context.Render<AddTransactionDialog>(p => p.Add(x => x.Visible, true).Add(x => x.InitialKind, TransactionKind.Investment));

        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<InvestmentTransactionFormBody>()));

        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Empty(cut.FindComponent<InvestmentTransactionFormBody>().FindAll("[role=dialog]"));
    }

    [Fact]
    public async Task CancelClosesTheDialog()
    {
        await using var context = CreateContext();
        bool? visible = null;
        var cut = context.Render<AddTransactionDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.VisibleChanged, value => visible = value));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<AddCurrencyEntry>()));

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        Assert.False(visible);
    }

    [Fact]
    public async Task EscapeClosesTheDialog_AndOtherKeysDoNot()
    {
        await using var context = CreateContext();
        bool? visible = null;
        var cut = context.Render<AddTransactionDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.VisibleChanged, value => visible = value));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<AddCurrencyEntry>()));

        await cut.Find("[data-testid=add-transaction-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });
        Assert.Null(visible);
        await cut.Find("[data-testid=add-transaction-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.False(visible);
    }

    [Fact]
    public async Task OpeningTheDialog_MovesFocusIntoIt()
    {
        await using var context = CreateContext();
        var cut = context.Render<AddTransactionDialog>(p => p.Add(x => x.Visible, false));

        cut.Render(p => p.Add(x => x.Visible, true));

        cut.WaitForAssertion(() => context.JSInterop.VerifyFocusAsyncInvoke());
    }

    [Fact]
    public async Task ASavedInvestment_RaisesOnSaved_AndCloses()
    {
        await using var context = CreateContext();
        var saved = 0;
        bool? visible = null;
        var cut = context.Render<AddTransactionDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.InitialKind, TransactionKind.Investment)
            .Add(x => x.OnSaved, () => saved++)
            .Add(x => x.VisibleChanged, value => visible = value));
        var body = cut.FindComponent<InvestmentTransactionFormBody>();

        await cut.InvokeAsync(() => body.Instance.OnSaved.InvokeAsync());

        Assert.Equal(1, saved);
        Assert.False(visible);
    }

    private static Task Select(IRenderedComponent<AddTransactionDialog> cut, string text) =>
        cut.InvokeAsync(() => cut.FindComponents<MudToggleItem<TransactionKind>>().Single(item => item.Instance.Text == text).Find("button, .mud-toggle-item").Click());

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = RecordingHttpHandler.ForEntryForms().CreateClient();
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Services.AddSingleton(new FinancialLabelHttpClient(http));
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        context.Services.AddSingleton(new InvestmentTradesHttpClient(http));
        context.Services.AddSingleton(new InvestmentTransactionHttpClient(http));
        context.Services.AddSingleton<AccountDataSynchronizationService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return context;
    }
}