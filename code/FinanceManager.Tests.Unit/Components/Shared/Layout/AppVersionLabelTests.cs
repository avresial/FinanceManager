using Bunit;
using FinanceManager.Components;
using FinanceManager.Components.Shared.Layout;

namespace FinanceManager.Tests.Unit.Components.Shared.Layout;

[Trait("Category", "Unit")]
public class AppVersionLabelTests
{
    [Fact]
    public void Renders_ApplicationVersion_InAppBarCaption()
    {
        using var context = new BunitContext();

        var cut = context.Render<AppVersionLabel>();

        var label = cut.Find("[data-testid='app-version']");
        Assert.Equal(ApplicationVersion.Version, label.TextContent);
        Assert.Contains("fm-appbar-version", label.ClassList);
    }
}