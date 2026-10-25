using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Shared.Components.Loading;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class RecurringTransactionDetectorCardViewTests
{
    [Fact]
    public async Task Overview_GroupsIncomeAndOutgoingWithSignedTotalsAndNetSubtitle()
    {
        await using var context = CreateContext();
        var salary = Item("Salary", 5_000m, isIncome: true);
        var bonus = Item("Bonus", 500m, isIncome: true);
        var rent = Item("Rent", 1_500m);
        var groceries = Item("Groceries", 300m);

        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.TotalMonthlySpend, 999_999m)
            .Add(x => x.Data, [rent, salary, groceries, bonus]));

        var card = cut.Find("[data-testid=recurring-transactions-card]");
        Assert.Equal("Net +3,700.00 PLN/mo · 5,500.00 in · 1,800.00 out", card.QuerySelector("[data-testid=recurring-subtitle]")!.TextContent.Trim());

        var income = card.QuerySelector("[data-testid=recurring-income-group]")!;
        Assert.Contains("Income", income.TextContent);
        Assert.Contains("+5,500.00 PLN/mo", income.TextContent);
        Assert.Equal(["Salary", "Bonus"], income.QuerySelectorAll("[title]").Select(x => x.GetAttribute("title")));
        Assert.All(income.QuerySelectorAll(".mud-progress-linear"), bar => Assert.Contains("mud-progress-linear-color-success", bar.ClassList));
        Assert.All(income.QuerySelectorAll("button .mud-typography:last-child"), amount => Assert.Contains("mud-success-text", amount.ClassList));

        var outgoing = card.QuerySelector("[data-testid=recurring-outgoing-group]")!;
        Assert.Contains("Outgoing", outgoing.TextContent);
        Assert.Contains("-1,800.00 PLN/mo", outgoing.TextContent);
        Assert.Equal(["Rent", "Groceries"], outgoing.QuerySelectorAll("[title]").Select(x => x.GetAttribute("title")));
        Assert.All(outgoing.QuerySelectorAll(".mud-progress-linear"), bar => Assert.Contains("mud-progress-linear-color-error", bar.ClassList));
        Assert.All(outgoing.QuerySelectorAll("button .mud-typography:last-child"), amount => Assert.Contains("mud-error-text", amount.ClassList));
        Assert.Empty(card.QuerySelectorAll("section svg"));
        Assert.Empty(card.QuerySelectorAll(".mud-warning-text, .mud-progress-linear-color-warning"));
        Assert.Contains("+5,000.00 PLN/mo", income.QuerySelector("button")!.TextContent);
        Assert.Contains("-1,500.00 PLN/mo", outgoing.QuerySelector("button")!.TextContent);
    }

    [Theory]
    [InlineData(1000, 100, "+900.00 PLN/mo")]
    [InlineData(100, 1000, "-900.00 PLN/mo")]
    [InlineData(500, 500, "0.00 PLN/mo")]
    public async Task Overview_ShowsSignedNetWithDirectionColor(decimal income, decimal outgoing, string expectedNet)
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.Data, [Item("Income", income, true), Item("Out", outgoing)]));

        var subtitle = cut.Find("[data-testid=recurring-transactions-card]");
        Assert.Contains(expectedNet, subtitle.TextContent);
        var net = subtitle.QuerySelector("[data-testid=recurring-net]")!;
        if (income == outgoing)
        {
            Assert.DoesNotContain("mud-success-text", net.ClassList);
            Assert.DoesNotContain("mud-error-text", net.ClassList);
        }
        else
        {
            Assert.Contains(income > outgoing ? "mud-success-text" : "mud-error-text", net.ClassList);
        }
    }

    [Fact]
    public async Task Overview_WithNoIncomeShowsOnlyOutgoingUnsignedSubtitleAndHidesEmptyGroup()
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.Data, [Item("Rent", 1_250m)]));

        var card = cut.Find("[data-testid=recurring-transactions-card]");
        Assert.Equal("Outgoing 1,250.00 PLN/mo", card.QuerySelector("[data-testid=recurring-subtitle]")!.TextContent.Trim());
        Assert.Empty(card.QuerySelectorAll("[data-testid=recurring-income-group]"));
        Assert.NotNull(card.QuerySelector("[data-testid=recurring-outgoing-group]"));
        Assert.Contains("1,250.00 PLN/mo", card.TextContent);
    }

    [Fact]
    public async Task Overview_WithIncomeOnlyHidesOutgoingGroup()
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.Data, [Item("Salary", 3_000m, true)]));

        Assert.NotNull(cut.Find("[data-testid=recurring-income-group]"));
        Assert.Empty(cut.FindAll("[data-testid=recurring-outgoing-group]"));
    }

    [Fact]
    public async Task Overview_ProgressBarsAreScaledIndependentlyPerGroupIncludingZeroValues()
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Data, [Item("Salary", 2_000m, true), Item("Small income", 500m, true), Item("Rent", 1_000m), Item("Coffee", 250m)]));

        Assert.Equal("100", ProgressFor(cut, "Salary"));
        Assert.Equal("25", ProgressFor(cut, "Small income"));
        Assert.Equal("100", ProgressFor(cut, "Rent"));
        Assert.Equal("25", ProgressFor(cut, "Coffee"));

        var zeroes = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Data, [Item("Zero income", 0, true), Item("Zero expense", 0)]));
        Assert.All(zeroes.FindAll("[role=progressbar]"), bar => Assert.Equal("0", bar.GetAttribute("aria-valuenow")));
    }

    [Fact]
    public async Task Overview_ItemSelectionReturnsSelectedItem()
    {
        await using var context = CreateContext();
        RecurringTransactionResult? selected = null;
        var salary = Item("Salary", 2_000m, true);
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Data, [salary])
            .Add(x => x.OnItemSelected, item => selected = item));

        await cut.Find("[data-testid=recurring-income-group] button").ClickAsync();

        Assert.Same(salary, selected);
    }

    [Theory]
    [InlineData(true, "+200.00 PLN", "mud-success-text")]
    [InlineData(false, "-200.00 PLN", "mud-error-text")]
    public async Task DetailView_ShowsSignedAmountsAndInvokesBackCallback(bool isIncome, string amount, string color)
    {
        await using var context = CreateContext();
        var backInvoked = false;
        var selected = Item("Selected", 2_000m, isIncome);
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.SelectedItem, selected)
            .Add(x => x.DetailEntries, [new CurrencyAccountEntry(1, 1, new DateTime(2026, 1, 1), 0, -200m)])
            .Add(x => x.OnBack, () => backInvoked = true));

        Assert.Contains(amount, cut.Markup);
        Assert.Contains(color, cut.Markup);
        await cut.Find("button[aria-label='Back to recurring transactions']").ClickAsync();
        Assert.True(backInvoked);
        Assert.Empty(cut.FindAll("a[href='/Subscriptions']"));
    }

    [Fact]
    public async Task Overview_RetryButtonInvokesCallbackOnError()
    {
        await using var context = CreateContext();
        var retryInvoked = false;
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.HasError, true)
            .Add(x => x.OnRetry, () => retryInvoked = true));

        await cut.FindAll("button").Single(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).ClickAsync();
        Assert.True(retryInvoked);
    }

    [Fact]
    public async Task DetailView_RetryButtonInvokesCallbackOnError()
    {
        await using var context = CreateContext();
        var retryInvoked = false;
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.SelectedItem, Item("Salary", 2_000m, true))
            .Add(x => x.HasDetailError, true)
            .Add(x => x.OnRetryDetail, () => retryInvoked = true));

        await cut.FindAll("button").Single(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).ClickAsync();
        Assert.True(retryInvoked);
    }

    [Fact]
    public async Task LoadingAndEmptyStatesRemainVisible()
    {
        await using var context = CreateContext();
        var loading = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters.Add(x => x.IsLoading, true));
        Assert.Single(loading.FindComponents<ChartSpinner>());

        var empty = context.Render<RecurringTransactionDetectorCardView>();
        Assert.Contains("No recurring transactions", empty.Markup);
    }

    [Fact]
    public async Task ListView_LinksToSubscriptionsPage()
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Data, [Item("Rent", 1_000m)]));

        var viewAll = cut.Find("a[href='/Subscriptions']");
        Assert.Equal("View all", viewAll.TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid=scroll-fade-area]"));
    }

    [Fact]
    public async Task ListView_ViewAllButtonLivesInCardHeader()
    {
        await using var context = CreateContext();
        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Data, [Item("Rent", 1_000m)]));

        // The "View all" control must live in the header action slot (top-right), not in the body.
        var card = cut.Find("[data-testid=recurring-transactions-card]");
        var header = card.QuerySelector("[data-testid=card-header]")!;
        var viewAll = header.QuerySelector("a[href='/Subscriptions']")!;
        Assert.Equal("View all", viewAll.TextContent.Trim());
        Assert.Equal("See all recurring transactions", viewAll.GetAttribute("aria-label"));
        Assert.Null(card.QuerySelector("[data-testid=scroll-fade-area] a[href='/Subscriptions']"));
    }

    private static string? ProgressFor(IRenderedComponent<RecurringTransactionDetectorCardView> cut, string title) =>
        cut.FindAll("[title]").Single(element => element.GetAttribute("title") == title)
            .ParentElement!.ParentElement!.QuerySelector("[role=progressbar]")!.GetAttribute("aria-valuenow");

    private static RecurringTransactionResult Item(string name, decimal value, bool isIncome = false) =>
        new(name, value) { IsIncome = isIncome };

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}