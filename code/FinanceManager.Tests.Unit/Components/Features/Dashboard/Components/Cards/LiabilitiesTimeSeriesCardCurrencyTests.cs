using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

/// <summary>Liabilities are summed unconverted, so the headline must not pose as the display currency (PLN) when all debt is in another one.</summary>
[Trait("Category", "Unit")]
public class LiabilitiesTimeSeriesCardCurrencyTests
{
    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ForeignOnlyDebt_ShowsItsOwnCurrency_WithoutTheNotConvertedNote()
    {
        await using var context = Context(["USD"]);

        var cut = context.Render<LiabilitiesTimeSeriesCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _start.AddDays(6)));

        cut.WaitForAssertion(() => Assert.Equal("325.00 USD", cut.Find(".fm-tsvc-value").TextContent.Trim()));
        Assert.Empty(cut.FindAll("[data-testid=unconverted-chip]"));
    }

    [Fact]
    public async Task MixedCurrencies_KeepTheDisplayCurrency()
    {
        await using var context = Context(["USD", "EUR"]);

        var cut = context.Render<LiabilitiesTimeSeriesCard>(p => p.Add(x => x.StartDateTime, _start).Add(x => x.EndDateTime, _start.AddDays(6)));

        cut.WaitForAssertion(() => Assert.Equal("325.00 PLN", cut.Find(".fm-tsvc-value").TextContent.Trim()));
    }

    private static BunitContext Context(string[] debtCurrencies)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        var coordinator = new SnapshotRefreshCoordinator(Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(coordinator);
        context.Services.AddSingleton(new LiabilitiesSnapshotStore(coordinator));
        var http = new HttpClient(new Backend(debtCurrencies)) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new LiabilitiesHttpClient(http));
        context.Services.AddSingleton(new UnconvertedCurrenciesHttpClient(http));
        return context;
    }

    private sealed class Backend(string[] debtCurrencies) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            object data = path.Contains("GetLiabilitiesTimeSeries", StringComparison.Ordinal)
                ? new List<TimeSeriesModel> { new(_start, -325m) }
                : path.Contains("GetEndLiabilitiesPerAccount", StringComparison.Ordinal)
                ? debtCurrencies.Select(code => new NameValueResult(code, -1m) { CurrencyCode = code }).ToList()
                : new List<string> { "USD" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data, data.GetType()) });
        }
    }
}