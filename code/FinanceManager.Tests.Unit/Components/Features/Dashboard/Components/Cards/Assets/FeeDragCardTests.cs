using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class FeeDragCardTests
{
    [Fact]
    public async Task PopulatedCard_RendersAnnualFeeMissingTerAndFixedHorizons()
    {
        await using var context = CreateContext();
        var cut = context.Render<FeeDragCard>();
        cut.Instance.Analysis = new FeeDragAnalysisResult
        {
            TotalHoldingsValue = 100_000m,
            MissingTerHoldingsValue = 20_000m,
            MissingTerCount = 1,
            TotalHoldingsCount = 3,
            AnnualFeeCost = 250m,
            WeightedExpenseRatio = 0.0025m,
        };
        cut.Render();

        Assert.Contains(250m.ToString("N2", CultureInfo.CurrentCulture), cut.Find("[data-testid='fee-drag-annual-fee']").TextContent);
        Assert.Contains("Expense ratio not set", cut.Find("[data-testid='fee-drag-missing-ter']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-10']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-20']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-30']"));
        Assert.Contains($"Avg TER {FeeDragCard.FormatPercentage(0.0025m)}", cut.Markup);
    }

    [Fact]
    public async Task ReturnRate_IsClampedAndRecalculatesProjections()
    {
        await using var context = CreateContext();
        var cut = context.Render<FeeDragCard>();
        cut.Instance.Analysis = new FeeDragAnalysisResult
        {
            TotalHoldingsValue = 100_000m,
            TotalHoldingsCount = 1,
            WeightedExpenseRatio = 0.005m,
        };
        cut.Instance.OnReturnRateChanged(0.02m);
        var initial = cut.Instance.ComputedProjections[0].CumulativeFeeCost;

        cut.Instance.OnReturnRateChanged(0.50m);
        Assert.Equal(0.10m, cut.Instance.AssumedAnnualReturnRate);
        Assert.NotEqual(initial, cut.Instance.ComputedProjections[0].CumulativeFeeCost);

        cut.Instance.OnReturnRateChanged(-0.50m);
        Assert.Equal(-0.10m, cut.Instance.AssumedAnnualReturnRate);
    }

    [Fact]
    public async Task EmptyCard_RendersEmptyState()
    {
        await using var context = CreateContext();
        var cut = context.Render<FeeDragCard>();

        Assert.NotNull(cut.Find("[data-testid='fee-drag-empty']"));
        Assert.Contains("No ETF holdings found", cut.Markup);
    }

    [Fact]
    public async Task AsOfDateChange_ReloadsOnceAndIgnoresStaleResponse()
    {
        var firstDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var secondDate = firstDate.AddDays(1);
        var handler = new QueuedFeeDragHandler();
        await using var context = CreateContext(handler, new UserSession
        {
            UserId = 1,
            UserName = "tester",
            Password = string.Empty,
            UserRole = UserRole.User,
        });

        var cut = context.Render<FeeDragCard>(parameters => parameters.Add(component => component.AsOfDate, firstDate));
        Assert.Equal(1, handler.RequestCount);

        cut.Render(parameters => parameters.Add(component => component.AsOfDate, secondDate));
        Assert.Equal(2, handler.RequestCount);

        handler.Complete(1, 200m);
        cut.WaitForAssertion(() => Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost));

        cut.Render(parameters => parameters.Add(component => component.AsOfDate, secondDate));
        Assert.Equal(2, handler.RequestCount);

        var renderCount = cut.RenderCount;
        handler.Complete(0, 100m);
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Equal(200m, cut.Instance.Analysis?.AnnualFeeCost);
    }

    private static BunitContext CreateContext(HttpMessageHandler? handler = null, UserSession? user = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(user);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(new AssetsHttpClient(new HttpClient(handler ?? new HttpClientHandler())
        {
            BaseAddress = new Uri("http://localhost/"),
        }));

        return context;
    }

    private sealed class QueuedFeeDragHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage>[] _responses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Complete(int index, decimal annualFeeCost) =>
            _responses[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new FeeDragAnalysisResult { AnnualFeeCost = annualFeeCost }),
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _requestCount) - 1;
            return _responses[index].Task;
        }
    }
}