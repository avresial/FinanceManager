using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Portfolio.Components;
using FinanceManager.Components.Features.Portfolio.Models;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Tests.Unit.Components.Features.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Portfolio.Components;

[Trait("Category", "Unit")]
public class PortfolioCardsTests
{
    private static readonly DateTime _end = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task Holdings_ReloadByDate_OrderSignedPositionsByValue_AndClearSoldPositions()
    {
        InvestmentHoldingDto[] rows =
        [
            new(1, "SHORT", "London", AssetTypeDto.Stock, -2, "GBP", 10m),
            new(2, "FUND", "Berlin", AssetTypeDto.ETF, 3, "EUR", 20m),
            new(3, "FLAT", "Berlin", AssetTypeDto.ETF, 0, "EUR", 50m)
        ];
        using var handler = new RecordingHttpHandler(r => Json(r.RequestUri!.Query == "?asOf=2026-11-20" ? rows : []));
        await using var context = CreateContext(handler);
        var cut = context.Render<InvestmentHoldingsCardContainer>(p => p.Add(c => c.AsOf, _end));
        var holdings = cut.FindComponent<InvestmentHoldingCard>().Instance.Holdings;
        Assert.Equal(["FUND", "SHORT"], holdings.Select(h => h.Ticker));
        Assert.Equal([60m, -20m], holdings.Select(h => h.Value));
        Assert.Equal(["EUR", "GBP"], holdings.Select(h => h.Currency));
        cut.Render(p => p.Add(c => c.AsOf, _end.AddDays(1)));
        Assert.Equal("/Transactions?dialog=add-entry&kind=investment", cut.Find("a").GetAttribute("href"));
        _snapshots.Verify(s => s.SetAsync("investment-holdings:v1:-7",
            It.Is<PortfolioCardSnapshot<List<InvestmentHoldingModel>>>(v => v.End == _end.AddDays(1) && v.Model.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task Holdings_CanExpandAndCollapseTheCompactList()
    {
        using var handler = new RecordingHttpHandler();
        await using var context = CreateContext(handler);
        var rows = Enumerable.Range(1, 4)
            .Select(id => new InvestmentHoldingModel(id, $"ETF{id}", "London", "GBP", 1, 100, 100)).ToList();
        var cut = context.Render<InvestmentHoldingCard>(p => p.Add(c => c.Holdings, rows));

        Assert.Single(cut.FindAll(".fm-holdings-compact"));
        var toggle = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Show all 4");
        await cut.InvokeAsync(() => toggle.Click());
        Assert.Empty(cut.FindAll(".fm-holdings-compact"));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Show less").Click());
        Assert.Single(cut.FindAll(".fm-holdings-compact"));
    }

    [Fact]
    public async Task Holdings_LoadFailure_StaysInsideTheCardAndRetryRecovers()
    {
        var fail = true;
        InvestmentHoldingDto[] rows = [new(2, "FUND", "Berlin", AssetTypeDto.ETF, 3, "EUR", 20m)];
        using var handler = new RecordingHttpHandler(_ => fail ? new(HttpStatusCode.ServiceUnavailable) : Json(rows));
        await using var context = CreateContext(handler);

        var cut = context.Render<InvestmentHoldingsCardContainer>(p => p.Add(c => c.AsOf, _end).Add(c => c.Height, "390px"));

        Assert.Single(cut.FindAll(".fm-holdings-card"));
        Assert.Contains("Couldn't load holdings.", cut.Find(".fm-holdings-card .mud-alert").TextContent);
        Assert.Equal("--fm-card-height:390px", cut.Find(".fm-holdings-sizer--sized").GetAttribute("style"));
        fail = false;
        await cut.InvokeAsync(() => cut.Find(".fm-holdings-card .mud-alert button").Click());
        Assert.Empty(cut.FindAll(".mud-alert"));
        Assert.Equal(["FUND"], cut.FindComponent<InvestmentHoldingCard>().Instance.Holdings.Select(h => h.Ticker));
    }

    [Fact]
    public async Task Holdings_StaleRowsKeepTheirCardAndShowACompactAlert_AndTotalsStayPerCurrency()
    {
        using var handler = new RecordingHttpHandler();
        await using var context = CreateContext(handler);
        InvestmentHoldingModel[] rows = [new(1, "A", "X", "USD", 1, 100, 100), new(2, "B", "X", "USD", 1, 50, 50), new(3, "C", "X", "EUR", 1, 20, 20)];

        var cut = context.Render<InvestmentHoldingCard>(p => p.Add(c => c.Holdings, rows).Add(c => c.HasError, true));

        Assert.Single(cut.FindAll(".fm-holdings-card"));
        Assert.Contains("Couldn't refresh holdings.", cut.Find(".fm-holdings-card .mud-alert").TextContent);
        Assert.Equal(3, cut.FindAll(".fm-holding-item").Count);
        Assert.Equal("20.00 EUR · 150.00 USD", cut.Find(".fm-holdings-totals .mud-typography-body2").TextContent.Trim());
    }

    [Fact]
    public async Task Value_AfterAFullExit_HidesTheGainChipAndSaysThereAreNoOpenPositions()
    {
        using var handler = new RecordingHttpHandler(r => r.RequestUri!.AbsolutePath switch
        {
            var path when path.Contains("/GetInvestmentGainLoss/") => new(HttpStatusCode.NoContent),
            var path when path.Contains("/ValueSeries/") => Json(new Dictionary<DateTime, decimal> { [_end.AddDays(-3)] = 100, [_end] = 0 }),
            _ => Json(new Dictionary<DateTime, decimal>())
        });
        await using var context = CreateContext(handler);

        var cut = context.Render<InvestmentsValueCardContainer>(p => p.Add(c => c.StartDateTime, _end.AddDays(-30)).Add(c => c.EndDateTime, _end));

        var chart = cut.FindComponent<TimeSeriesValueCard>().Instance;
        Assert.Null(chart.DeltaBaseline);
        Assert.Empty(cut.FindAll(".fm-tsvc-delta"));
        Assert.Equal("No open positions", chart.SummaryCaption);
        Assert.Equal("PLN", chart.CurrencyShortName);
    }

    [Fact]
    public async Task Value_RetriesFailedSeries_AndRefreshesOwnerChartUnderStableKey()
    {
        bool fail = true;
        using var handler = new RecordingHttpHandler(r => r.RequestUri!.AbsolutePath switch
        {
            var path when path.Contains("/GetInvestmentGainLoss/") => Json(new UnrealizedGainLossAccountResult(null, "Investments", 100, 125, 25, 25, _end, 0)),
            var path when path.Contains("/CapitalSeries/") => Json(new Dictionary<DateTime, decimal> { [_end.AddDays(-2)] = 100 }),
            var path when path.Contains("/BenchmarkSeries/") => Json(new Dictionary<DateTime, decimal> { [_end] = 110 }),
            var path when path.Contains("/ValueSeries/") => fail ? new(HttpStatusCode.ServiceUnavailable) :
                Json(new Dictionary<DateTime, decimal> { [_end.AddDays(-3)] = 0, [_end] = 125 }),
            _ => throw new InvalidOperationException(r.RequestUri.AbsolutePath)
        });
        await using var context = CreateContext(handler);
        var refresh = new List<bool>();
        var cut = context.Render<InvestmentsValueCardContainer>(p => p.Add(c => c.StartDateTime, _end.AddDays(-30))
            .Add(c => c.EndDateTime, _end).Add(c => c.RefreshingChanged, (bool v) => refresh.Add(v)));
        Assert.True(cut.FindComponent<TimeSeriesValueCard>().Instance.HasError);
        fail = false;
        await cut.InvokeAsync(() => cut.FindComponent<TimeSeriesValueCard>().Instance.OnRetry.InvokeAsync());
        var chart = cut.FindComponent<TimeSeriesValueCard>().Instance;
        Assert.False(chart.HasError);
        Assert.Equal(125, Assert.Single(chart.Data).Value);
        Assert.Equal(100m, chart.DeltaBaseline);
        Assert.Equal(["Capital", "Capital + inflation"], chart.Overlays.Select(o => o.Name));
        Assert.Equal(_end, chart.Overlays[0].Data[^1].DateTime);
        cut.Render(p => p.Add(c => c.StartDateTime, _end.AddDays(-7)));
        Assert.Equal(12, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.DoesNotContain("account", r.PathAndQuery, StringComparison.OrdinalIgnoreCase));
        Assert.Equal([true, false, true, false, true, false], refresh);
        _snapshots.Verify(s => s.SetAsync($"investments-value:v1:-7:{DefaultCurrency.PLN.Id}:inflation",
            It.Is<PortfolioCardSnapshot<InvestmentsValueModel>>(v => v.Start == _end.AddDays(-7))), Times.Once);
    }

    private BunitContext CreateContext(RecordingHttpHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLogging();
        var login = new Mock<ILoginService>();
        login.Setup(s => s.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = -7, UserName = "guest", Password = "", UserRole = UserRole.User });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        var http = handler.CreateClient();
        var settings = new UserSettingsService(login.Object, Mock.Of<IUserService>(), new CurrencyHttpClient(http), new InvestmentTransactionHttpClient(http), NullLogger<UserSettingsService>.Instance);
        settings.SetCurrency(DefaultCurrency.PLN);
        settings.SetBenchmark(null);
        context.Services.AddSingleton(settings);
        context.Services.AddSingleton<ISettingsService>(settings);
        context.Services.AddSingleton(new AssetsHttpClient(http));
        context.Services.AddSingleton(new InvestmentValuationHttpClient(http));
        context.Services.AddSingleton(new InvestmentTradesHttpClient(http));
        return context;
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}