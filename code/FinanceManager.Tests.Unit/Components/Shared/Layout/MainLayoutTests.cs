using Bunit;
using FinanceManager.Components;
using FinanceManager.Components.Shared.Layout;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Layout;

[Trait("Category", "Unit")]
public class MainLayoutTests
{
    [Theory]
    [InlineData(Breakpoint.Lg)]
    [InlineData(Breakpoint.Xs)]
    public async Task Version_IsBelowAccountActions_OnlyInOpenDrawer(Breakpoint breakpoint)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.AddAuthorization().SetAuthorized("guest");
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<ILoginService>());
        context.Services.AddSingleton(Mock.Of<IUserService>());
        context.Services.AddSingleton(Mock.Of<ISettingsService>());
        context.ComponentFactories.AddStub<NavMenu>();
        var viewport = new Mock<IBrowserViewportService>();
        viewport.Setup(v => v.SubscribeAsync(It.IsAny<Guid>(),
                It.IsAny<Func<BrowserViewportEventArgs, Task>>(), It.IsAny<ResizeOptions>(), true))
            .Returns((Guid id, Func<BrowserViewportEventArgs, Task> callback, ResizeOptions options, bool immediate) =>
                callback(new BrowserViewportEventArgs(id, new BrowserWindowSize(), breakpoint, true)));
        context.Services.AddSingleton(viewport.Object);

        var cut = context.Render<MainLayout>();
        var mobile = breakpoint == Breakpoint.Xs;
        Assert.Empty(cut.FindAll("header [data-testid='app-version']"));
        if (mobile)
        {
            Assert.Empty(cut.FindAll("[data-testid='app-version']"));
            cut.Find("[aria-label='Toggle navigation menu']").Click();
        }

        var label = cut.Find("aside [data-testid='app-version']");
        Assert.Equal(ApplicationVersion.Version, label.TextContent);
        Assert.True(cut.Markup.IndexOf("nav-logout", StringComparison.Ordinal) <
                    cut.Markup.IndexOf("app-version", StringComparison.Ordinal));
        Assert.Equal(mobile ? DrawerVariant.Temporary : DrawerVariant.Mini, cut.FindComponent<MudDrawer>().Instance.Variant);

        cut.Find("[aria-label='Toggle navigation menu']").Click();
        Assert.Empty(cut.FindAll("[data-testid='app-version']"));
        cut.Find("[aria-label='Toggle navigation menu']").Click();
        Assert.Single(cut.FindAll("aside [data-testid='app-version']"));
    }
}