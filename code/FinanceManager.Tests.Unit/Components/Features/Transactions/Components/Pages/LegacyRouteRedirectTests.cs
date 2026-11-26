using Bunit;
using FinanceManager.Components.Features.Transactions.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Components.Pages;

[Trait("Category", "Unit")]
public class LegacyRouteRedirectTests
{
    [Theory]
    [InlineData("/AccountDetails/7", "/Transactions")]
    [InlineData("/ManageAccount/7", "/Transactions")]
    [InlineData("/Import/7", "/Transactions")]
    [InlineData("/Export/7", "/Transactions")]
    [InlineData("/AddAccount", "/Transactions?dialog=add-entry")]
    [InlineData("/AddAccount/", "/Transactions?dialog=add-entry")]
    [InlineData("/AddAccount/?source=review", "/Transactions?dialog=add-entry")]
    [InlineData("/Import/7/", "/Transactions")]
    public async Task OldAccountRoutes_ReplaceTheHistoryEntryWithTheirTransactionsTarget(string oldRoute, string expectedPath)
    {
        await using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(oldRoute);

        context.Render<LegacyRouteRedirect>(p => p.Add(x => x.AccountId, 7));

        Assert.EndsWith(expectedPath, navigation.Uri);
        var last = context.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().History.First();
        Assert.True(last.Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void Page_ClaimsEveryOldAccountRoute()
    {
        var templates = typeof(LegacyRouteRedirect).GetCustomAttributes<RouteAttribute>().Select(route => route.Template).Order().ToArray();

        Assert.Equal(["/AccountDetails/{AccountId:int}", "/AddAccount", "/Export/{AccountId:int}", "/Import/{AccountId:int}", "/ManageAccount/{AccountId:int}"], templates);
    }
}