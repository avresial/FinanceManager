using Bunit;
using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components;

[Trait("Category", "Unit")]
public sealed class TransactionLogToolbarTests
{
    private static readonly TransactionLogAccountOption[] _accounts =
    [
        new(1, "Everyday", AccountType.Currency),
        new(2, "Savings", AccountType.Currency),
        new(3, "Treasury bonds", AccountType.Bond),
        new(4, "Broker", AccountType.Stock),
    ];

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

        var cut = RenderToolbar(context, new([AccountType.Bond], []), _ => { });

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
        Assert.Equal([AccountType.Stock], raised.AccountTypes);
        Assert.Empty(raised.AccountIds);
    }

    [Fact]
    public async Task ClickingAnotherChip_AddsItToTheSelection()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([AccountType.Currency], []), filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Equal([AccountType.Currency, AccountType.Bond], raised.AccountTypes);
    }

    [Fact]
    public async Task DeselectingAType_PrunesSelectedAccountsThatBecomeHidden()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([AccountType.Currency, AccountType.Bond], [1, 3]), filter => raised = filter);

        // Deselect "Bonds": the bond account 3 is no longer offered, so it leaves the filter.
        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Equal([AccountType.Currency], raised.AccountTypes);
        Assert.Equal([1], raised.AccountIds);
    }

    [Fact]
    public async Task DeselectingTheLastType_KeepsSelectedAccounts_BecauseEveryAccountIsOfferedAgain()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([AccountType.Bond], [3]), filter => raised = filter);

        cut.FindAll(".fm-txlog-type-chip")[1].Click();

        Assert.NotNull(raised);
        Assert.Empty(raised.AccountTypes);
        Assert.Equal([3], raised.AccountIds);
    }

    [Fact]
    public async Task AccountSelect_OffersAllAccounts_WhenNoTypeIsSelected()
    {
        await using var context = CreateContext();
        var cut = RenderToolbar(context, TransactionLogFilter.None, _ => { });

        OpenAccountSelect(cut);

        Assert.Equal(["Broker", "Everyday", "Savings", "Treasury bonds"], AccountOptionTexts(cut));
    }

    [Fact]
    public async Task AccountSelect_IsNarrowedToTheSelectedTypes()
    {
        await using var context = CreateContext();
        var cut = RenderToolbar(context, new([AccountType.Currency, AccountType.Bond], []), _ => { });

        OpenAccountSelect(cut);

        Assert.Equal(["Everyday", "Savings", "Treasury bonds"], AccountOptionTexts(cut));
    }

    [Fact]
    public async Task PickingAnAccount_RaisesFilterChangedWithItsId_AndKeepsTheTypes()
    {
        await using var context = CreateContext();
        TransactionLogFilter? raised = null;
        var cut = RenderToolbar(context, new([AccountType.Currency], []), filter => raised = filter);
        OpenAccountSelect(cut);

        cut.FindAll(".mud-list-item").Single(item => item.TextContent.Contains("Savings")).Click();

        Assert.NotNull(raised);
        Assert.Equal([AccountType.Currency], raised.AccountTypes);
        Assert.Equal([2], raised.AccountIds);
    }

    [Fact]
    public async Task AccountSelect_ShowsTheSelectedAccountName()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, new([], [4]), _ => { });

        Assert.Equal("Broker", cut.Find("input[data-testid='transactions-account-filter']").GetAttribute("value"));
    }

    [Theory]
    [InlineData(new int[0], "")]
    [InlineData(new[] { 1, 4 }, "2 accounts")]
    public async Task AccountSelect_SummarisesTheSelection_AndStaysEmptyWithoutOne(int[] selectedIds, string expectedText)
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, new([], selectedIds), _ => { });

        Assert.Equal(expectedText, cut.Find("input[data-testid='transactions-account-filter']").GetAttribute("value"));
    }

    [Fact]
    public async Task AccountSelect_IsDisabled_WhenThereAreNoAccounts()
    {
        await using var context = CreateContext();

        var cut = RenderToolbar(context, TransactionLogFilter.None, _ => { }, accounts: []);

        Assert.NotNull(cut.Find("input[data-testid='transactions-account-filter']").GetAttribute("disabled"));
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

    private static void OpenAccountSelect(IRenderedComponent<IComponent> cut) =>
        cut.Find(".fm-txlog-account-select").MouseDown();

    private static string[] AccountOptionTexts(IRenderedComponent<IComponent> cut) =>
        [.. cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())];

    private static IRenderedComponent<IComponent> RenderToolbar(
        BunitContext context,
        TransactionLogFilter filter,
        Action<TransactionLogFilter> onChanged,
        IReadOnlyList<TransactionLogAccountOption>? accounts = null) =>
        context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TransactionLogToolbar>(1);
            builder.AddComponentParameter(2, nameof(TransactionLogToolbar.Accounts), accounts ?? _accounts);
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