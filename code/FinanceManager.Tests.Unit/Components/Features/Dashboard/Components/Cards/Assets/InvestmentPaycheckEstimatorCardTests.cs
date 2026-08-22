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
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class InvestmentPaycheckEstimatorCardTests
{
    private static InvestmentPaycheckEstimatorCard RenderCard(BunitContext context, InvestmentPaycheckEstimate estimate, decimal rate)
    {
        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        Assert.Contains("Investment paycheck", cut.Markup);
        var card = cut.Instance;
        card._estimate = estimate;
        card._annualWithdrawalRate = rate;
        return card;
    }

    [Fact]
    public async Task MonthlyPaycheck_ComputesLocallyFromInvestableValueAndRate()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        // 120000 * 0.04 / 12 = 400
        Assert.Equal(400m, card.MonthlyPaycheck);
    }

    [Fact]
    public async Task OnPresetSelected_RecalculatesPaycheckLocally_WithoutFetching()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        card.OnPresetSelected(0.05m);

        Assert.Equal(0.05m, card._annualWithdrawalRate);
        // 120000 * 0.05 / 12 = 500
        Assert.Equal(500m, card.MonthlyPaycheck);
    }

    [Fact]
    public async Task OnRateChanged_RecalculatesPaycheckLocally_WithoutFetching()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate { InvestableAssetsValue = 240_000m }, 0.04m);

        card.OnRateChanged(0.03m);

        Assert.Equal(0.03m, card._annualWithdrawalRate);
        // 240000 * 0.03 / 12 = 600
        Assert.Equal(600m, card.MonthlyPaycheck);
    }

    [Fact]
    public async Task ReplacementRatio_DerivesFromLocalPaycheckAndAverageSalary()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 120_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 800m,
        }, 0.04m);

        // paycheck 400 / salary 800 = 0.5
        Assert.Equal(0.5m, card.ReplacementRatio);
    }

    [Fact]
    public async Task ReplacementRatio_TracksRateChangesLocally()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 120_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 800m,
        }, 0.04m);

        card.OnPresetSelected(0.08m);

        // paycheck 800 / salary 800 = 1.0
        Assert.Equal(1.0m, card.ReplacementRatio);
    }

    [Fact]
    public async Task MonthlyPaycheck_And_ReplacementRatio_RoundToServerPrecision()
    {
        // Matches InvestmentPaycheckEstimatorService: paycheck rounded to 2 decimals,
        // ratio rounded to 4 decimals from that rounded paycheck.
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate
        {
            InvestableAssetsValue = 100_000m,
            SalaryMonthsUsed = 3,
            AverageMonthlySalary = 3_000m,
        }, 0.05m);

        // 100000 * 0.05 / 12 = 416.66666... -> 416.67
        Assert.Equal(416.67m, card.MonthlyPaycheck);
        // 416.67 / 3000 = 0.138890 -> 0.1389
        Assert.Equal(0.1389m, card.ReplacementRatio);
    }

    [Fact]
    public async Task ReplacementRatio_IsNull_WhenNoSalaryData()
    {
        await using var context = CreateContext();
        var card = RenderCard(context, new InvestmentPaycheckEstimate { InvestableAssetsValue = 120_000m }, 0.04m);

        Assert.Null(card.ReplacementRatio);
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

        context.Services.AddSingleton(new InvestmentPaycheckEstimateCacheService(
            Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient()),
            NullLogger<InvestmentPaycheckEstimateCacheService>.Instance));

        return context;
    }
}