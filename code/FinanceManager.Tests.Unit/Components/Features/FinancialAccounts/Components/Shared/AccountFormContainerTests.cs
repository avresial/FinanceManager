using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Shared.Components;
using Microsoft.AspNetCore.Components.Sections;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class AccountFormContainerTests
{
    [Theory]
    [InlineData("Import")]
    [InlineData("Export")]
    [InlineData("Add account")]
    [InlineData("Manage account")]
    public void CentersFormContentWhileKeepingTitleInAppBar(string title)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var outlet = context.Render<SectionOutlet>(parameters => parameters.Add(section => section.SectionName, "page-title"));
        var cut = context.Render<AccountFormContainer>(parameters => parameters.Add(container => container.ChildContent, builder =>
        {
            builder.OpenComponent<PageHeader>(0);
            builder.AddAttribute(1, nameof(PageHeader.Title), title);
            builder.CloseComponent();
            builder.AddMarkupContent(2, "<form><input aria-label=\"Account name\" /></form>");
        }));

        Assert.Equal(MaxWidth.Small, cut.FindComponent<MudContainer>().Instance.MaxWidth);
        Assert.Empty(cut.FindAll("h1"));
        var heading = Assert.Single(outlet.FindAll("h1"));
        Assert.Equal(title, heading.TextContent);
        Assert.Equal("-1", heading.GetAttribute("tabindex"));
        Assert.NotNull(cut.Find(".fm-account-form form input"));
    }
}