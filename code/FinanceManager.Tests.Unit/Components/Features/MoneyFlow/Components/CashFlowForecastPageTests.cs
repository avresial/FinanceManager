using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.MoneyFlow.Components;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.MoneyFlow.Components;

[Trait("Category", "Unit")]
public class CashFlowForecastPageTests
{
    [Fact]
    public async Task SelectingNewHorizon_CancelsPreviousLoadAndKeepsLatestForecast()
    {
        var handler = new ForecastHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<CashFlowForecastPage>();
        cut.WaitForAssertion(() => Assert.Contains("INITIAL", cut.Markup));

        var staleSelection = FindHorizon(cut, 60).ClickAsync(new());
        await handler.SixtyDayRequestStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            Xunit.TestContext.Current.CancellationToken);

        await FindHorizon(cut, 30).ClickAsync(new());
        await staleSelection;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("LATEST", cut.Markup);
            Assert.DoesNotContain("STALE", cut.Markup);
            Assert.DoesNotContain("mud-skeleton", cut.Markup);
        });
        Assert.True(handler.SixtyDayRequestCancelled.Task.IsCompletedSuccessfully);
    }

    private static AngleSharp.Dom.IElement FindHorizon(IRenderedComponent<CashFlowForecastPage> cut, int horizonDays) =>
        cut.FindAll("button").Single(button => button.TextContent.Contains($"{horizonDays} days", StringComparison.Ordinal));

    private static BunitContext CreateContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ApexChart<TimeSeriesModel>>();
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User
        });
        context.Services.AddSingleton(login.Object);

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new CashFlowForecastHttpClient(httpClient));
        return context;
    }

    private sealed class ForecastHandler : HttpMessageHandler
    {
        public TaskCompletionSource SixtyDayRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SixtyDayRequestCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("horizonDays=60", StringComparison.Ordinal))
            {
                SixtyDayRequestStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    SixtyDayRequestCancelled.TrySetResult();
                    throw;
                }

                return Forecast("STALE", 60);
            }

            return query.Contains("horizonDays=30", StringComparison.Ordinal)
                ? Forecast("LATEST", 30)
                : Forecast("INITIAL", 90);
        }

        private static HttpResponseMessage Forecast(string currency, int horizonDays) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CashFlowForecast
            {
                Currency = currency,
                HorizonDays = horizonDays,
                AsOfDate = new DateTime(2026, 9, 16),
                HistoricalSeries = [new TimeSeriesModel(new DateTime(2026, 9, 16), 100m)],
                ForecastSeries = [new TimeSeriesModel(new DateTime(2026, 9, 16), 100m)]
            })
        };
    }
}