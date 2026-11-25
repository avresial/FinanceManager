using Bunit;
using FinanceManager.Components.Features.App.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.App.Components;

[Trait("Category", "Unit")]
public class WelcomePageTests
{
    [Theory]
    [InlineData("welcome-add-cash", "Transactions?dialog=add-entry&kind=cash")]
    [InlineData("welcome-add-bond", "Transactions?dialog=add-entry&kind=bond")]
    [InlineData("welcome-add-investment", "Transactions?dialog=add-entry&kind=investment")]
    [InlineData("welcome-import-csv", "Transactions/Import/cash")]
    [InlineData("welcome-import-bond-csv", "Transactions/Import/bond")]
    public async Task Tiles_OpenTheTransactionsFlows(string testId, string expectedRelativeUri)
    {
        await using var context = CreateContext();
        var cut = context.Render<WelcomePage>();

        cut.Find($"[data-testid={testId}]").Click();

        Assert.EndsWith("/" + expectedRelativeUri, context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri);
    }

    [Fact]
    public async Task Page_OffersTransactionsNotAccounts()
    {
        await using var context = CreateContext();

        var cut = context.Render<WelcomePage>();

        Assert.Contains("Add your first transaction", cut.Markup);
        Assert.DoesNotContain("AddAccount", cut.Markup);
        Assert.DoesNotContain("account", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cut.FindAll("[data-testid=welcome-cash] button"), button => button.TextContent.Contains("Import CSV"));
        Assert.Contains(cut.FindAll("[data-testid=welcome-bond] button"), button => button.TextContent.Contains("Import CSV"));
        // Investment CSV import is out of scope, and every card adds with the same verb.
        Assert.DoesNotContain("Import", cut.Find("[data-testid=welcome-investment]").TextContent);
        Assert.All(new[] { "welcome-add-cash", "welcome-add-bond", "welcome-add-investment" },
            id => Assert.Equal("Add transaction", cut.Find($"[data-testid={id}]").TextContent.Trim()));
        // The dashboard preview appears once, as the first carousel slide, not also as a hero image.
        Assert.Empty(cut.FindAll(".onb-hero"));
        Assert.Single(cut.FindAll("img.onb-light[src$='dashboard-light.jpg']"));
        Assert.All(cut.FindAll(".onb-carousel"), image => Assert.DoesNotContain("currency-", image.GetAttribute("src")));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}