using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.App.Components;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Tests.Unit.Components.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using DashboardComponent = FinanceManager.Components.Features.Dashboard.Components.Dashboard;

namespace FinanceManager.Tests.Unit.Components.Features.App.Components;

[Trait("Category", "Unit")]
public class HomeTests
{
    [Fact]
    public async Task GuestWithTransactions_OpensTheDashboard()
    {
        var backend = new NavMenuTestBackend(transactionCount: 1);
        await using var context = CreateContext(backend, userId: -5);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<DashboardComponent>>()));
        Assert.Empty(cut.FindComponents<Stub<WelcomePage>>());
        Assert.Equal("api/TransactionLog/Get/-5?count=1", Assert.Single(backend.Requests, path => path.StartsWith("api/TransactionLog/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UserWithNoTransactions_OpensTheWelcomePage()
    {
        await using var context = CreateContext(new NavMenuTestBackend(transactionCount: 0), userId: 3);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<WelcomePage>>()));
        Assert.Empty(cut.FindComponents<Stub<DashboardComponent>>());
    }

    [Fact]
    public async Task CachedHasTransactions_OpensTheDashboardWithoutAnyRequest()
    {
        var backend = new NavMenuTestBackend(transactionCount: 0);
        CacheSnapshot(backend, new NavMenuCacheSnapshot { UserId = 3, FetchedAtUtc = DateTime.UtcNow, HasTransactions = true });
        await using var context = CreateContext(backend, userId: 3);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<DashboardComponent>>()));
        Assert.Empty(backend.Requests);
    }

    [Fact]
    public async Task CachedNoTransactions_IsCheckedAgain_SoTheFirstAddShowsTheDashboard()
    {
        var backend = new NavMenuTestBackend(transactionCount: 1);
        CacheSnapshot(backend, new NavMenuCacheSnapshot { UserId = 3, FetchedAtUtc = DateTime.UtcNow, HasTransactions = false });
        await using var context = CreateContext(backend, userId: 3);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<DashboardComponent>>()));
        Assert.Equal(1, backend.TransactionRequests);
    }

    [Fact]
    public async Task FailedCheck_ShowsARetryAlertThatReloads()
    {
        var backend = new NavMenuTestBackend(failTransactions: true);
        await using var context = CreateContext(backend, userId: 3);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load your data", cut.Markup));
        Assert.Empty(cut.FindComponents<Stub<DashboardComponent>>());
        Assert.Empty(cut.FindComponents<Stub<WelcomePage>>());
        Assert.Equal(1, backend.TransactionRequests);

        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, backend.TransactionRequests));
    }

    private static void CacheSnapshot(NavMenuTestBackend backend, NavMenuCacheSnapshot snapshot) =>
        backend.Storage.Setup(x => x.GetItemAsync<NavMenuCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<NavMenuCacheSnapshot?>(snapshot));

    private static BunitContext CreateContext(NavMenuTestBackend backend, int userId)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.ComponentFactories.AddStub<DashboardComponent>();
        context.ComponentFactories.AddStub<WelcomePage>();

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = userId, UserName = userId < 0 ? "guest" : "user", Password = string.Empty, UserRole = UserRole.User, Token = "t" });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(backend.CreateService());
        context.Services.AddLogging();
        return context;
    }
}