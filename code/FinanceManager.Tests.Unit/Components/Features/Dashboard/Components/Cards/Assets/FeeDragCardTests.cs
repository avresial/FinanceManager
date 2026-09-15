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

        Assert.Contains("250.00", cut.Find("[data-testid='fee-drag-annual-fee']").TextContent);
        Assert.Contains("Expense ratio not set", cut.Find("[data-testid='fee-drag-missing-ter']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-10']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-20']"));
        Assert.NotNull(cut.Find("[data-testid='fee-drag-projection-30']"));
        Assert.Contains("Avg TER 0.3%", cut.Markup);
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

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync((UserSession?)null);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(new AssetsHttpClient(new HttpClient()));

        return context;
    }
}