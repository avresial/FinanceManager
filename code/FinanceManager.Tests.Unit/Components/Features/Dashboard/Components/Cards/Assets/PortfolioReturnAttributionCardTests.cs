using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class PortfolioReturnAttributionCardTests
{
    [Fact]
    public async Task AvailableResult_RendersSortedVerticalWaterfallWithoutDuplicateBreakdown()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(6);
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler);

        var cut = context.Render<PortfolioReturnAttributionCardContainer>(parameters => parameters
            .Add(component => component.StartDateTime, start)
            .Add(component => component.EndDateTime, end));
        handler.Complete(0, PortfolioReturnAttributionResult.Available(
            1554.81m, 0m, -51.18m, 0m, 1605.99m, start, end));

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("[data-testid='return-attribution-column']").Count));
        var categories = cut.FindAll("[data-testid='return-attribution-category']");
        Assert.Equal(
            ["FX effect", "External cash movement", "Known transaction fees", "Market / valuation effect", "End"],
            categories.Select(category => category.TextContent.Trim()));

        var values = cut.FindAll("[data-testid='return-attribution-value']");
        Assert.Equal(
            [
                $"+{1605.99m.ToString("N2", CultureInfo.CurrentCulture)}",
                0m.ToString("N2", CultureInfo.CurrentCulture),
                0m.ToString("N2", CultureInfo.CurrentCulture),
                $"-{51.18m.ToString("N2", CultureInfo.CurrentCulture)}",
                1554.81m.ToString("N2", CultureInfo.CurrentCulture),
            ],
            values.Select(value => value.TextContent.Trim()));

        var columns = cut.FindAll("[data-testid='return-attribution-column']");
        Assert.Equal("0", columns[0].GetAttribute("data-start"));
        Assert.Equal("1605.99", columns[0].GetAttribute("data-end"));
        Assert.Equal("1605.99", columns[3].GetAttribute("data-start"));
        Assert.Equal("1554.81", columns[3].GetAttribute("data-end"));
        Assert.Equal("1554.81", columns[4].GetAttribute("data-end"));
        Assert.Equal(
            ["0", "500", "1.0k", "1.5k", "2.0k"],
            cut.FindAll(".attribution-axis-label").Select(label => label.TextContent.Trim()));

        Assert.Contains($"+{1554.81m.ToString("N2", CultureInfo.CurrentCulture)} PLN", cut.Find("[data-testid='return-attribution-total']").TextContent);
        Assert.DoesNotContain("return-attribution-breakdown", cut.Markup);

        var summary = cut.Find("[data-testid='return-attribution-chart']").GetAttribute("aria-label");
        Assert.Contains($"running total +{1605.99m.ToString("N2", CultureInfo.CurrentCulture)} PLN", summary);
        Assert.Contains($"running total +{1554.81m.ToString("N2", CultureInfo.CurrentCulture)} PLN", summary);
        Assert.Contains("Reconciliation difference: 0.00 PLN", cut.Markup);
        Assert.Contains("Unavailable: Dividends / income; ETF expense-ratio fees", cut.Markup);
    }

    [Fact]
    public async Task RangeChange_IgnoresSlowerPreviousResponse()
    {
        var firstStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstEnd = firstStart.AddDays(6);
        var secondStart = firstStart.AddDays(7);
        var secondEnd = secondStart.AddDays(6);
        var handler = new QueuedAssetsHandler();
        var snapshots = new Mock<ISnapshotService>();
        await using var context = CreateContext(handler, snapshots.Object);

        var cut = context.Render<PortfolioReturnAttributionCardContainer>(parameters => parameters
            .Add(component => component.StartDateTime, firstStart)
            .Add(component => component.EndDateTime, firstEnd));
        Assert.Equal(1, handler.ReturnAttributionRequestCount);

        cut.Render(parameters => parameters
            .Add(component => component.StartDateTime, secondStart)
            .Add(component => component.EndDateTime, secondEnd));
        Assert.Equal(2, handler.ReturnAttributionRequestCount);

        handler.Complete(1, PortfolioReturnAttributionResult.Available(200m, 0m, 200m, 0m, 0m, secondStart, secondEnd));
        var currentAmount = $"+{200m.ToString("N2", CultureInfo.CurrentCulture)} PLN";
        cut.WaitForAssertion(() => Assert.Contains(currentAmount, cut.Markup));

        var renderCount = cut.RenderCount;
        handler.Complete(0, PortfolioReturnAttributionResult.Available(100m, 0m, 100m, 0m, 0m, firstStart, firstEnd));
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Contains(currentAmount, cut.Markup);
        Assert.DoesNotContain($"+{100m.ToString("N2", CultureInfo.CurrentCulture)} PLN", cut.Markup);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(),
            It.Is<PortfolioReturnAttributionCardSnapshot>(snapshot => snapshot.Model!.TotalChange == 100m)), Times.Never);
        cut.WaitForAssertion(() => snapshots.Verify(service => service.SetAsync(It.IsAny<string>(),
            It.Is<PortfolioReturnAttributionCardSnapshot>(snapshot => snapshot.Model!.TotalChange == 200m)), Times.Once));
    }

    private static readonly DateTime _start = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddDays(6);
    private static readonly string _key = $"portfolio-return-attribution:1:{DefaultCurrency.PLN.Id}";

    [Theory]
    [InlineData("equal")]
    [InlineData("changed")]
    [InlineData("failure")]
    [InlineData("empty")]
    [InlineData("unavailable")]
    public async Task Hydration_AlwaysRefreshes_AndReconcilesRenderedContent(string outcome)
    {
        var snapshots = Snapshots(Available(150m));
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler, snapshots.Object);
        var cut = Render(context);
        cut.WaitForAssertion(() => Assert.Contains(Amount(150m), cut.Markup));
        Assert.Equal(1, handler.ReturnAttributionRequestCount);
        var renders = cut.RenderCount;
        if (outcome == "failure") handler.Fail(0);
        else handler.Complete(0, outcome switch
        {
            "changed" => Available(250m),
            "empty" => PortfolioReturnAttributionResult.InsufficientData(_start, _end),
            "unavailable" => PortfolioReturnAttributionResult.Unavailable(_start, _end),
            // These source fields are not rendered and must not trigger a write.
            _ => Available(150m) with { StartDate = _start.AddHours(1), DividendIncome = 42m },
        });
        cut.WaitForState(() => cut.RenderCount > renders);
        var changed = outcome is "changed" or "empty" or "unavailable";
        cut.WaitForAssertion(() => Assert.Contains(outcome switch
        {
            "changed" => Amount(250m),
            "empty" => "Insufficient data",
            "unavailable" => "Attribution unavailable",
            _ => Amount(150m),
        }, cut.Markup));
        Assert.DoesNotContain("Could not load return attribution", cut.Markup);
        cut.WaitForAssertion(() => snapshots.Verify(service => service.SetAsync(_key,
            It.IsAny<PortfolioReturnAttributionCardSnapshot>()), changed ? Times.Once : Times.Never));
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task FailedFirstLoad_ShowsError_RetryFetchesAgain()
    {
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler);
        var cut = Render(context);
        handler.Fail(0);
        cut.WaitForAssertion(() => Assert.Contains("Could not load return attribution", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).Click();
        // The retry fetch is dispatched asynchronously after the click.
        cut.WaitForAssertion(() => Assert.Equal(2, handler.ReturnAttributionRequestCount));
        handler.Complete(1, Available(250m));
        cut.WaitForAssertion(() => Assert.Contains(Amount(250m), cut.Markup));
    }

    [Fact]
    public async Task StorageFailure_DoesNotPreventFreshPainting()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<PortfolioReturnAttributionCardSnapshot>(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("read"));
        snapshots.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnAttributionCardSnapshot>())).ThrowsAsync(new InvalidOperationException("write"));
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler, snapshots.Object);
        var cut = Render(context);
        handler.Complete(0, Available(250m));
        cut.WaitForAssertion(() => Assert.Contains(Amount(250m), cut.Markup));
        Assert.DoesNotContain("Could not load return attribution", cut.Markup);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("currency")]
    [InlineData("range")]
    public async Task WrongSnapshotScope_DoesNotPaint(string mismatch)
    {
        var snapshot = Snapshot(Available(150m));
        if (mismatch == "user") snapshot.UserId++;
        if (mismatch == "currency") snapshot.CurrencyId++;
        if (mismatch == "range") snapshot.StartDateTime = _start.AddDays(-1);
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<PortfolioReturnAttributionCardSnapshot>(It.IsAny<string>())).ReturnsAsync(snapshot);
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler, snapshots.Object);
        var cut = Render(context);
        Assert.DoesNotContain(Amount(150m), cut.Markup);
        handler.Complete(0, Available(250m));
        cut.WaitForAssertion(() => Assert.Contains(Amount(250m), cut.Markup));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCards_ShareRequests_KeepSeparateSnapshots_VisitFetchesAgain(bool hydrate)
    {
        var handler = new QueuedAssetsHandler();
        var snapshots = hydrate ? Snapshots(Available(150m)) : new Mock<ISnapshotService>();
        if (hydrate)
        {
            snapshots.Setup(service => service.GetAsync<PortfolioReturnCardSnapshot>(It.IsAny<string>())).ReturnsAsync(new PortfolioReturnCardSnapshot
            {
                UserId = 1,
                CurrencyId = DefaultCurrency.PLN.Id,
                StartDateTime = _start,
                EndDateTime = _end,
                Model = new(null, MoneyWeightedReturnStatus.InsufficientData, null, TimeWeightedReturnStatus.InsufficientData, 0m),
            });
        }
        await using var context = CreateContext(handler, snapshots.Object);
        var returns = context.Render<PortfolioReturnCardContainer>(parameters => parameters
            .Add(component => component.StartDateTime, _start).Add(component => component.EndDateTime, _end));
        var attribution = Render(context);
        Assert.Equal(1, handler.ReturnAttributionRequestCount);
        Assert.Equal(1, handler.MoneyWeightedRequestCount);
        Assert.Equal(1, handler.TimeWeightedRequestCount);
        handler.Complete(0, Available(250m));
        attribution.WaitForAssertion(() => Assert.Contains(Amount(250m), attribution.Markup));
        returns.WaitForAssertion(() => snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<PortfolioReturnCardSnapshot>()), hydrate ? Times.Never : Times.Once));
        attribution.WaitForAssertion(() => snapshots.Verify(service => service.SetAsync(_key, It.IsAny<PortfolioReturnAttributionCardSnapshot>()), Times.Once));
        attribution.Dispose();
        var nextVisit = Render(context);
        Assert.Equal(2, handler.ReturnAttributionRequestCount);
        handler.Complete(1, Available(350m));
        nextVisit.WaitForAssertion(() => Assert.Contains(Amount(350m), nextVisit.Markup));
    }

    private static IRenderedComponent<PortfolioReturnAttributionCardContainer> Render(BunitContext context) =>
        context.Render<PortfolioReturnAttributionCardContainer>(parameters => parameters
            .Add(component => component.StartDateTime, _start).Add(component => component.EndDateTime, _end));
    private static string Amount(decimal amount) => $"+{amount.ToString("N2", CultureInfo.CurrentCulture)} PLN";
    private static PortfolioReturnAttributionResult Available(decimal amount) =>
        PortfolioReturnAttributionResult.Available(amount, 0m, amount, 0m, 0m, _start, _end);
    private static PortfolioReturnAttributionCardSnapshot Snapshot(PortfolioReturnAttributionResult result) => new()
    {
        UserId = 1,
        CurrencyId = DefaultCurrency.PLN.Id,
        StartDateTime = _start,
        EndDateTime = _end.AddHours(1),
        Model = PortfolioReturnAttributionCardModel.FromResult(result),
    };
    private static Mock<ISnapshotService> Snapshots(PortfolioReturnAttributionResult result)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<PortfolioReturnAttributionCardSnapshot>(It.IsAny<string>())).ReturnsAsync(Snapshot(result));
        return snapshots;
    }

    private static BunitContext CreateContext(HttpMessageHandler handler, ISnapshotService? snapshots = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(snapshots ?? Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance));
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

    private sealed class QueuedAssetsHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage>[] _returnAttributionResponses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private int _returnAttributionRequestCount;
        public int MoneyWeightedRequestCount { get; private set; }
        public int TimeWeightedRequestCount { get; private set; }

        public int ReturnAttributionRequestCount => Volatile.Read(ref _returnAttributionRequestCount);

        public void Fail(int index) => _returnAttributionResponses[index].SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        public void Complete(int index, PortfolioReturnAttributionResult result) =>
            _returnAttributionResponses[index].SetResult(Response(result));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("GetReturnAttribution", StringComparison.Ordinal))
            {
                var index = Interlocked.Increment(ref _returnAttributionRequestCount) - 1;
                return _returnAttributionResponses[index].Task;
            }

            if (path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal))
            {
                MoneyWeightedRequestCount++;
                return Task.FromResult(Response(new MoneyWeightedReturnResult(
                    null,
                    MoneyWeightedReturnStatus.InsufficientData,
                    DateTime.MinValue,
                    DateTime.MinValue)));

            }
            if (path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal))
            {
                TimeWeightedRequestCount++;
                return Task.FromResult(Response(new TimeWeightedReturnResult(
                    null,
                    TimeWeightedReturnStatus.InsufficientData,
                    DateTime.MinValue,
                    DateTime.MinValue)));

            }
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