using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Shared.Components;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class AccountFormContainerTests
{
    [Fact]
    public void SharesExportsSmallWidthWithHeadingAndFormContent()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var cut = context.Render<AccountFormContainer>(parameters => parameters.Add(container => container.ChildContent, builder =>
        {
            builder.OpenComponent<PageHeader>(0);
            builder.AddAttribute(1, nameof(PageHeader.Title), "Add account");
            builder.AddAttribute(2, nameof(PageHeader.InContent), true);
            builder.CloseComponent();
            builder.AddMarkupContent(3, "<form><input aria-label=\"Account name\" /></form>");
        }));

        Assert.Equal(MaxWidth.Small, cut.FindComponent<MudContainer>().Instance.MaxWidth);
        Assert.Equal("Add account", cut.Find(".fm-account-form h1").TextContent);
        Assert.NotNull(cut.Find(".fm-account-form form input"));
    }
}