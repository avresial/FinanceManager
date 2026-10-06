using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Identity.Services;

[Trait("Category", "Unit")]
public class UserSettingsServiceTests
{
    private const string _currenciesPath = "/api/Currency/GetAll";

    [Fact]
    public async Task GetCurrencyAsync_ConcurrentCalls_ShareOneResolution()
    {
        var gate = new TaskCompletionSource();
        var handler = new RequestCountingHandler(_ => new[] { new Currency(5, "EUR", "€") }, gate.Task);
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetUser(1)).ReturnsAsync(CreateUser(preferredCurrencyId: 5));
        var service = CreateService(handler, LoggedIn(), userService);

        var first = service.GetCurrencyAsync();
        var second = service.GetCurrencyAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, handler.GetCount(_currenciesPath));
        userService.Verify(s => s.GetUser(1), Times.Once);
        Assert.Equal("EUR", (await second).ShortName);
    }

    [Fact]
    public async Task GetCurrencyAsync_AfterResolved_ServedFromCache()
    {
        var handler = new RequestCountingHandler(_ => new[] { new Currency(5, "EUR", "€") });
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetUser(1)).ReturnsAsync(CreateUser(preferredCurrencyId: 5));
        var service = CreateService(handler, LoggedIn(), userService);

        await service.GetCurrencyAsync();
        await service.GetCurrencyAsync();

        Assert.Equal(1, handler.GetCount(_currenciesPath));
    }

    [Fact]
    public async Task GetCurrencyAsync_WhenLoggedOut_FallsBackWithoutPinningTheFallback()
    {
        var handler = new RequestCountingHandler(_ => new[] { new Currency(5, "EUR", "€") });
        var loginService = new Mock<ILoginService>();
        loginService.SetupSequence(s => s.GetLoggedUser())
            .ReturnsAsync((UserSession?)null)
            .ReturnsAsync(new UserSession { UserId = 1, UserName = "user", Password = "", UserRole = UserRole.User });
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetUser(1)).ReturnsAsync(CreateUser(preferredCurrencyId: 5));
        var service = CreateService(handler, loginService, userService);

        var loggedOut = await service.GetCurrencyAsync();
        var loggedIn = await service.GetCurrencyAsync();

        Assert.Equal(DefaultCurrency.PLN.ShortName, loggedOut.ShortName);
        Assert.Equal("EUR", loggedIn.ShortName);
    }

    private static Mock<ILoginService> LoggedIn()
    {
        var loginService = new Mock<ILoginService>();
        loginService.Setup(s => s.GetLoggedUser())
            .ReturnsAsync(new UserSession { UserId = 1, UserName = "user", Password = "", UserRole = UserRole.User });
        return loginService;
    }

    private static User CreateUser(int preferredCurrencyId) => new()
    {
        UserId = 1,
        Login = "user",
        CreationDate = DateTime.UtcNow,
        PreferredCurrencyId = preferredCurrencyId,
    };

    private static UserSettingsService CreateService(RequestCountingHandler handler, Mock<ILoginService> loginService, Mock<IUserService> userService)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new UserSettingsService(
            loginService.Object,
            userService.Object,
            new CurrencyHttpClient(httpClient),
            new InvestmentTransactionHttpClient(httpClient),
            NullLogger<UserSettingsService>.Instance);
    }
}