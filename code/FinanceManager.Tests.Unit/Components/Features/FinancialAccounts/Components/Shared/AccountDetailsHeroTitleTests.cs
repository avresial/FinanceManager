using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using Microsoft.AspNetCore.Components.Sections;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class AccountDetailsHeroTitleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowsAccountNameAsAppBarTitle(bool isMobile)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var outlet = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-title"));

        var cut = context.Render<AccountDetailsHero>(parameters => parameters
            .Add(hero => hero.AccountName, "Loan 1")
            .Add(hero => hero.Currency, "PLN")
            .Add(hero => hero.IsChartLoading, true)
            .Add(hero => hero.IsMobile, isMobile));

        var heading = Assert.Single(outlet.FindAll("h1"));
        Assert.Equal("Loan 1", heading.TextContent);
        Assert.DoesNotContain(cut.FindAll("h1, h2, h3, h4, h5, h6"), element => element.TextContent.Trim() == "Loan 1");
    }
}