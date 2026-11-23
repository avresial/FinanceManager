using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Domain.Dashboard.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components;

[Trait("Category", "Unit")]
public sealed class TransactionLogToolbarTests
{
    private static readonly string[] _currencies = ["EUR", "PLN", "USD"];

    [Fact]
    public async Task Renders_CashBondsAndInvestmentsChips_NoneSelected()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, TransactionLogFilter.None, _ => { });

        var chips = cut.FindAll(".fm-txlog-type-chip");
        Assert.Equal(["Cash", "Bonds", "Investments"], chips.Select(chip => chip.TextContent.Trim()));
        Assert.All(chips, chip => Assert.DoesNotContain("mud-chip-selected", chip.ClassList));
    }

    [Fact]
    public async Task SelectedTypes_AreMarkedSelected()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, new([TransactionKind.Bond], []), _ => { });

        var selected = cut.FindAll(".fm-txlog-type-chip.mud-chip-selected").Select(chip => chip.TextContent.Trim());
        Assert.Equal(["Bonds"], selected);
    }

    [Fact]
    public async Task ClickingAChip_RaisesFilterChangedWithThatType()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, TransactionLogFilter.None, filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[2].Click();

        Assert.NotNull(raised);
        Assert.Equal([TransactionKind.Investment], raised.Kinds);
        Assert.Empty(raised.Currencies);
    }

    [Fact]
    public async Task ClickingAnotherChip_AddsItToTheSelection()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([TransactionKind.Cash], []), filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Equal([TransactionKind.Cash, TransactionKind.Bond], raised.Kinds);
    }

    [Fact]
    public async Task DeselectingAType_KeepsSelectedCurrencies()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([TransactionKind.Cash, TransactionKind.Bond], ["EUR", "USD"]), filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Equal([TransactionKind.Cash], raised.Kinds);
        Assert.Equal(["EUR", "USD"], raised.Currencies);
    }

    [Fact]
    public async Task DeselectingTheLastType_KeepsSelectedAccounts_BecauseEveryAccountIsOfferedAgain()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([TransactionKind.Bond], ["USD"]), filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Empty(raised.Kinds);
        Assert.Equal(["USD"], raised.Currencies);
    }

    [Fact]
    public async Task CurrencySelect_OffersAllAccounts_WhenNoTypeIsSelected()
    {
        await using var context = CreateContext();
        var cut = RenderToolbar(context, TransactionLogFilter.None, _ => { });

        OpenCurrencySelect(cut);

        Assert.Equal(_currencies, CurrencyOptionTexts(cut));
    }

    [Fact]
    public async Task CurrencySelect_OffersCurrenciesRegardlessOfKind()
    {
        await using var context = CreateContext();
        var cut = RenderToolbar(context, new([TransactionKind.Cash, TransactionKind.Bond], []), _ => { });

        OpenCurrencySelect(cut);

        Assert.Equal(_currencies, CurrencyOptionTexts(cut));
    }

    [Fact]
    public async Task PickingAnAccount_RaisesFilterChangedWithItsId_AndKeepsTheTypes()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([TransactionKind.Cash], []), filter => raised = filter);
        OpenCurrencySelect(cut);

        cut.FindAll(".mud-list-item").Single(item => item.TextContent.Contains("PLN")).Click();

        Assert.NotNull(raised);
        Assert.Equal([TransactionKind.Cash], raised.Kinds);
        Assert.Equal(["PLN"], raised.Currencies);
    }

    [Fact]
    public async Task CurrencySelect_ShowsTheSelectedAccountName()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, new([], ["USD"]), _ => { });

        Assert.Equal("USD", cut.Find("input[data-testid='transactions-currency-filter']").GetAttribute("value"));
    }

    [Fact]
    public async Task CurrencySelect_IsDisabled_WhenThereAreNoAccounts()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, TransactionLogFilter.None, _ => { }, currencies: []);

        Assert.NotNull(cut.Find("input[data-testid='transactions-currency-filter']").GetAttribute("disabled"));
    }

    [Fact]
    public async Task SearchPanelExpandsDebouncesAndHidesWithEscape()
    {
        await using var context = CreateContext();
        string? search = null;
        var cut = context.Render<TransactionLogToolbar>(p => p
            .Add(x => x.SearchTextChanged, value => search = value));
        Assert.False(cut.FindComponent<MudCollapse>().Instance.Expanded);
        cut.Find("button[aria-label='Search transactions']").Click();
        Assert.True(cut.FindComponent<MudCollapse>().Instance.Expanded);
        var field = cut.FindComponents<MudTextField<string>>().Single(x => x.Instance.DebounceInterval == 150);
        Assert.Equal(150, field.Instance.DebounceInterval);
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("mixed"));
        Assert.Equal("mixed", search);
        await cut.InvokeAsync(() => field.Instance.OnKeyDown.InvokeAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" }));
        Assert.False(cut.FindComponent<MudCollapse>().Instance.Expanded);
        Assert.Equal("false", cut.Find("button[aria-label='Search transactions']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task ExistingSearchIsExpandedOnFirstRender()
    {
        await using var context = CreateContext();
        var cut = context.Render<TransactionLogToolbar>(p => p.Add(x => x.SearchText, "mixed"));
        Assert.True(cut.FindComponent<MudCollapse>().Instance.Expanded);
    }

    private static void OpenCurrencySelect(IRenderedComponent<IComponent> cut) =>
        cut.Find(".fm-txlog-currency-select").MouseDown();

    private static string[] CurrencyOptionTexts(IRenderedComponent<IComponent> cut) =>
        [.. cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())];

    private static IRenderedComponent<IComponent> RenderToolbar(
        BunitContext context,
        TransactionLogFilter filter,
        Action<TransactionLogFilter> onChanged,
        IReadOnlyList<string>? currencies = null) =>
        context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TransactionLogToolbar>(1);
            builder.AddComponentParameter(2, nameof(TransactionLogToolbar.Currencies), currencies ?? _currencies);
            builder.AddComponentParameter(3, nameof(TransactionLogToolbar.Filter), filter);
            builder.AddComponentParameter(4, nameof(TransactionLogToolbar.FilterChanged),
                EventCallback.Factory.Create<TransactionLogFilter>(new object(), onChanged));
            builder.CloseComponent();
        });

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}