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
public class InvestmentRateCardTests
{
    [Fact]
    public async Task SelectMonth_UpdatesSelectionAndIgnoresPlaceholderBars()
    {
        var january = new InvestmentRate { Start = new DateTime(2026, 1, 1) };
        var february = new InvestmentRate { Start = new DateTime(2026, 2, 1) };
        await using var context = CreateContext();
        var card = RenderCard(context);
        card.AsOfDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        card.MonthlyInvestmentRates = [january, february];

        Assert.True(card.SelectMonth(0));
        Assert.Same(january, card.SelectedMonthRate);
        Assert.False(card.SelectMonth(2));
        Assert.Same(january, card.SelectedMonthRate);
    }

    [Fact]
    public async Task BuildDerivedState_CurrentMonthWithoutSalary_ReportsNoRate()
    {
        // The salary for the current month has not arrived yet, but investments were made. The card
        // must not show a rate for that month, must not plot a bar for it, and must keep it out of
        // the average — otherwise it reads as if the salary had already been received.
        var june = new InvestmentRate { Start = new DateTime(2026, 6, 1), Salary = 9456.88m, InvestmentsChange = 4000m };
        var july = new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 0m, InvestmentsChange = 5523.17m };
        await using var context = CreateContext();
        var card = RenderCard(context);
        card.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        card.MonthlyInvestmentRates = [june, july];

        card.BuildDerivedState();

        Assert.Null(card.CurrentMonthPercentage);
        Assert.Equal(4000m / 9456.88m, card.YtdAveragePercentage);
        Assert.Null(card.Series[1].Percentage);
        Assert.Equal(4000m / 9456.88m * 100m, card.Series[0].Percentage);
    }

    [Fact]
    public async Task BuildDerivedState_CurrentMonthWithSalary_ReportsRate()
    {
        var july = new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 10_000m, InvestmentsChange = 5000m };
        await using var context = CreateContext();
        var card = RenderCard(context);
        card.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        card.MonthlyInvestmentRates = [july];

        card.BuildDerivedState();

        Assert.Equal(0.5m, card.CurrentMonthPercentage);
        Assert.Equal(0.5m, card.YtdAveragePercentage);
        Assert.Equal(50m, card.Series[0].Percentage);
    }

    private static InvestmentRateCard RenderCard(BunitContext context)
    {
        var cut = context.Render<InvestmentRateCard>();
        Assert.Contains("Investment rate", cut.Markup);
        return cut.Instance;
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        // CardInfoTooltip renders a MudTooltip, which resolves MudBlazor.PopoverService — a
        // service that only implements IAsyncDisposable. Callers must dispose the context
        // with `await using`, or the synchronous container disposal throws.
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync((UserSession?)null);
        context.Services.AddSingleton(login.Object);

        context.Services.AddSingleton(new InvestmentRateCacheService(
            Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new MoneyFlowHttpClient(new HttpClient()),
            NullLogger<InvestmentRateCacheService>.Instance));

        return context;
    }
}