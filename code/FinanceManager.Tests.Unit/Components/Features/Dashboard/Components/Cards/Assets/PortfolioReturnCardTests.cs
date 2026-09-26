using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class PortfolioReturnCardTests
{
    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(6);

    [Fact]
    public async Task BothAvailable_ShowsMetricsAndFactualSummary()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            PortfolioReturnAttributionResult.Available(300m, 100m, 200m, 0m, 0m, _start, _end)));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));

        Assert.Contains("10.00%", cut.Markup);
        Assert.Contains("Net external inflows of", cut.Markup);
        Assert.Contains("Net external cash flow:", cut.Markup);
        Assert.Contains("Period length: 7 days", cut.Markup);
        Assert.Contains("Annualized · PLN", cut.Markup);
        Assert.Contains("Cumulative · PLN", cut.Markup);
        Assert.Equal(1, cut.Markup.Split("08/01/2026 – 08/07/2026").Length - 1);
        Assert.Equal(2, cut.FindAll(".portfolio-return-metric").Count);
    }

    [Fact]
    public async Task OneUnavailable_KeepsAvailableSibling()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(null, MoneyWeightedReturnStatus.InsufficientData, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            null));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("10.00%", cut.Markup));

        Assert.Contains("Insufficient data", cut.Markup);
        Assert.DoesNotContain("Neither return is available", cut.Markup);
        Assert.Contains("Cash-flow timing affects annualized XIRR", cut.Markup);
    }

    [Fact]
    public async Task BothUnavailable_ShowsUnifiedEmptyStateAndMetricReasons()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(null, MoneyWeightedReturnStatus.NoSolution, _start, _end),
            new(null, TimeWeightedReturnStatus.InsufficientData, _start, _end),
            null));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("Neither return is available", cut.Markup));

        Assert.Contains("No solution", cut.Markup);
        Assert.Contains("Insufficient data", cut.Markup);
    }

    [Fact]
    public async Task FailedMetricRequest_DoesNotDiscardSibling()
    {
        await using var context = CreateContext(new ReturnsHandler(
            new(0.2m, MoneyWeightedReturnStatus.Available, _start, _end),
            new(0.1m, TimeWeightedReturnStatus.Available, _start, _end),
            null,
            failMoneyWeightedOnce: true));

        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains("10.00%", cut.Markup));

        Assert.Contains("Could not load this return", cut.Markup);
        Assert.Contains("Retry unavailable return", cut.Markup);
        Assert.DoesNotContain("Neither return is available", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Retry unavailable return", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));
        Assert.Contains("10.00%", cut.Markup);
    }

    private static IRenderedComponent<PortfolioReturnCard> Render(BunitContext context) =>
        context.Render<PortfolioReturnCard>(parameters => parameters
            .Add(component => component.StartDateTime, _start)
            .Add(component => component.EndDateTime, _end));

    private static BunitContext CreateContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 1,
            UserName = "tester",
            Password = string.Empty,
            UserRole = UserRole.User,
        });
        context.Services.AddSingleton(login.Object);

        context.Services.AddSingleton(new AssetsPageCardsCacheService(
            Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<AssetsPageCardsCacheService>.Instance));

        return context;
    }

    private sealed class ReturnsHandler(
        MoneyWeightedReturnResult? moneyWeighted,
        TimeWeightedReturnResult timeWeighted,
        PortfolioReturnAttributionResult? attribution,
        bool failMoneyWeightedOnce = false) : HttpMessageHandler
    {
        private int _moneyWeightedRequestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal))
                return Task.FromResult(failMoneyWeightedOnce && Interlocked.Increment(ref _moneyWeightedRequestCount) == 1
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : Response(moneyWeighted));
            if (path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal))
                return Task.FromResult(Response(timeWeighted));
            if (path.Contains("GetReturnAttribution", StringComparison.Ordinal))
                return Task.FromResult(Response(attribution ?? PortfolioReturnAttributionResult.Unavailable(_start, _end)));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }

        private static HttpResponseMessage Response<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        };
    }
}