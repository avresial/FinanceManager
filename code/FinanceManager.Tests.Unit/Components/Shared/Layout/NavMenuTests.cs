using Bunit;
using FinanceManager.Components;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Components.Shared.Layout;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Tests.Unit.Components.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Layout;

[Trait("Category", "Unit")]
public class NavMenuTests
{
    private static BunitContext CreateContext(NavMenuTestBackend backend)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 5, UserName = "user", Password = string.Empty, UserRole = UserRole.User });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(new TransactionDataSynchronizationService());
        context.Services.AddSingleton(backend.CreateService());
        return context;
    }

    [Fact]
    public async Task TransactionsLink_PointsToTransactionsPage()
    {
        await using var context = CreateContext(new NavMenuTestBackend());

        var cut = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, true));

        var link = cut.Find("[data-testid='nav-transactions']");
        Assert.Equal("Transactions", link.TextContent.Trim());
        Assert.EndsWith("Transactions", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Menu_HasNoAccountsSection()
    {
        await using var context = CreateContext(new NavMenuTestBackend(hasAssets: true, hasLiabilities: true, transactionCount: 1));

        var cut = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, true));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='nav-assets']")));
        Assert.Empty(cut.FindAll("[data-testid='nav-add-account']"));
        Assert.DoesNotContain("Accounts", cut.Markup);
        Assert.DoesNotContain("AccountDetails", cut.Markup);
        Assert.DoesNotContain("AddAccount", cut.Markup);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task AssetsAndLiabilitiesLinks_FollowTheirFlags(bool hasAssets, bool hasLiabilities)
    {
        await using var context = CreateContext(new NavMenuTestBackend(hasAssets, hasLiabilities, transactionCount: 1));

        var cut = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(hasAssets, cut.FindAll("[data-testid='nav-assets']").Count == 1);
            Assert.Equal(hasLiabilities, cut.FindAll("[data-testid='nav-liabilities']").Count == 1);
        });
    }

    [Theory]
    [InlineData(true, "FinanceManager")]
    [InlineData(false, "FM")]
    public async Task Branding_ExposesVersionOnlyThroughFocusableTooltip(bool drawerIsOpen, string brandText)
    {
        await using var context = CreateContext(new NavMenuTestBackend());

        var cut = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, drawerIsOpen));

        var brand = cut.Find("[data-testid='nav-brand']");
        Assert.Equal(brandText, brand.TextContent);
        Assert.Equal($"FinanceManager, version {ApplicationVersion.Version}", brand.GetAttribute("aria-label"));
        Assert.Equal("0", brand.GetAttribute("tabindex"));
        Assert.Equal($"Version {ApplicationVersion.Version}", cut.FindComponent<MudTooltip>().Instance.Text);
        Assert.DoesNotContain(ApplicationVersion.Version, brand.TextContent);
        Assert.Empty(cut.FindAll("[data-testid='app-version']"));
    }
}