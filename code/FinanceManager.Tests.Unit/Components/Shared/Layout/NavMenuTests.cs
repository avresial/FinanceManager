using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Layout;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Layout;

[Trait("Category", "Unit")]
public class NavMenuTests
{
    [Fact]
    public async Task TransactionsLink_PointsToTransactionsPage()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<ILoginService>());
        context.Services.AddSingleton(new AccountDataSynchronizationService());
        context.Services.AddSingleton(new NavMenuStateCacheService(
            Mock.Of<ILocalStorageService>(), new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFinancialAccountService>(), new AssetsHttpClient(new HttpClient()),
            new LiabilitiesHttpClient(new HttpClient()), NullLogger<NavMenuStateCacheService>.Instance));

        var cut = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, true));

        var link = cut.Find("[data-testid='nav-transactions']");
        Assert.Equal("Transactions", link.TextContent.Trim());
        Assert.EndsWith("Transactions", link.GetAttribute("href"));
    }

    [Theory]
    [InlineData(true, "FinanceManager")]
    [InlineData(false, "FM")]
    public async Task Branding_ExposesVersionOnlyThroughFocusableTooltip(bool drawerIsOpen, string brandText)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<ILoginService>());
        context.Services.AddSingleton(new AccountDataSynchronizationService());
        context.Services.AddSingleton(new NavMenuStateCacheService(
            Mock.Of<ILocalStorageService>(), new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFinancialAccountService>(), new AssetsHttpClient(new HttpClient()),
            new LiabilitiesHttpClient(new HttpClient()), NullLogger<NavMenuStateCacheService>.Instance));

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