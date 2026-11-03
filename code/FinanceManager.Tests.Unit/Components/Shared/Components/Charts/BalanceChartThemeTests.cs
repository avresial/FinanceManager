using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Components.Charts;

[Trait("Category", "Unit")]
public class BalanceChartThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThemeOnlyChangeRepaintsBalanceChartAndRestoresDarkAmber(bool accountHero)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        List<TimeSeriesModel> points = [new(DateTime.Today, 100m), new(DateTime.Today.AddDays(1), 120m)];
        var cut = context.Render<CascadingValue<bool>>(p =>
        {
            p.Add(x => x.Name, "IsDarkMode").Add(x => x.Value, false);
            if (accountHero)
                p.AddChildContent<AccountDetailsHero>(child => child
                    .Add(x => x.AccountName, "Cash 1").Add(x => x.Currency, "PLN").Add(x => x.ChartData, points));
            else
                p.AddChildContent<TimeSeriesValueCard>(child => child.Add(x => x.Data, points));
        });

        var content = Assert.IsType<RenderFragment>(cut.Instance.ChildContent);
        foreach (var (dark, expected) in new[] { (false, "#D38D00"), (true, "#FFAB00"), (false, "#D38D00") })
        {
            var previous = cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance;
            var changed = cut.Instance.Value != dark;
            cut.Render(p => p.Add(x => x.Name, "IsDarkMode").Add(x => x.Value, dark).AddChildContent(content));
            var chart = cut.FindComponent<ApexChart<TimeSeriesModel>>().Instance;
            Assert.Equal(expected, chart.Options.Colors[0]);
            if (changed) Assert.NotSame(previous, chart);
            Assert.Equal(points.Select(x => x.Value), chart.Series[0].Items.Select(x => x.Value));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SemanticAndCustomColoursArePreserved(bool dark)
    {
        Assert.Equal(ColorsProvider.LiabilityColor, BalanceChartColors.ForTheme(AccountChartColors.BalanceColor(
            FinanceManager.Domain.FinancialAccounts.Shared.Entities.AccountLabel.Loan), dark));
        Assert.Equal("#42a5f5", BalanceChartColors.ForTheme("#42a5f5", dark));
        Assert.Equal("#66bb6a", BalanceChartColors.ForTheme("#66bb6a", dark));
        Assert.Equal(dark ? "#FFAB00" : "#D38D00", BalanceChartColors.ForTheme("#ffab00", dark));
    }
}