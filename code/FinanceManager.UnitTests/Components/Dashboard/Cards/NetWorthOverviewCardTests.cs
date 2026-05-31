using Blazored.LocalStorage;
using FinanceManager.Components.Components.Dashboard.Cards;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;

namespace FinanceManager.UnitTests.Components.Dashboard.Cards;

[Trait("Category", "Unit")]
public class NetWorthOverviewCardTests
{
    private static UserSession LoggedInUser => new()
    {
        UserId = 1,
        UserName = "user",
        Password = "password",
        UserRole = UserRole.User,
    };

    private static Mock<DashboardOverviewCardsCacheService> CreateCacheServiceMock()
    {
        // GetSnapshotAsync is virtual so the failure/success paths can be simulated; the base
        // constructor only stores its dependencies, so passing mocks/stubs here is safe.
        var localStorage = new Mock<ILocalStorageService>();
        var memoryCache = new Mock<IMemoryCache>();
        var moneyFlowClient = new MoneyFlowHttpClient(new HttpClient { BaseAddress = new Uri("http://localhost") });

        return new Mock<DashboardOverviewCardsCacheService>(
            localStorage.Object,
            memoryCache.Object,
            moneyFlowClient,
            NullLogger<DashboardOverviewCardsCacheService>.Instance);
    }

    private static NetWorthOverviewCard CreateCard(
        DashboardOverviewCardsCacheService cacheService,
        ISnackbar snackbar,
        ILoginService loginService)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetCurrency()).Returns(DefaultCurrency.PLN);

        return new NetWorthOverviewCard
        {
            Logger = NullLogger<NetWorthOverviewCard>.Instance,
            Snackbar = snackbar,
            DashboardOverviewCardsCacheService = cacheService,
            SettingsService = settings.Object,
            LoginService = loginService,
        };
    }

    [Fact]
    public async Task LoadAsync_WhenSnapshotFetchThrows_SetsErrorAndShowsSnackbar()
    {
        var login = new Mock<ILoginService>();
        login.Setup(l => l.GetLoggedUser()).ReturnsAsync(LoggedInUser);

        var cache = CreateCacheServiceMock();
        cache.Setup(c => c.GetSnapshotAsync(It.IsAny<DashboardOverviewCardsRefreshContext>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var snackbar = new Mock<ISnackbar>();

        var card = CreateCard(cache.Object, snackbar.Object, login.Object);

        await card.LoadAsync();

        Assert.True(card.HasError);
        snackbar.Verify(
            s => s.Add(It.IsAny<string>(), Severity.Error, It.IsAny<Action<SnackbarOptions>?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WhenSnapshotFetchSucceeds_DoesNotErrorOrNotify()
    {
        var login = new Mock<ILoginService>();
        login.Setup(l => l.GetLoggedUser()).ReturnsAsync(LoggedInUser);

        var cache = CreateCacheServiceMock();
        cache.Setup(c => c.GetSnapshotAsync(It.IsAny<DashboardOverviewCardsRefreshContext>()))
            .ReturnsAsync(new DashboardOverviewCardsCacheSnapshot { NetWorth = 4321.99m });

        var snackbar = new Mock<ISnackbar>();

        var card = CreateCard(cache.Object, snackbar.Object, login.Object);

        await card.LoadAsync();

        Assert.False(card.HasError);
        snackbar.Verify(
            s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>?>(), It.IsAny<string>()),
            Times.Never);
    }
}