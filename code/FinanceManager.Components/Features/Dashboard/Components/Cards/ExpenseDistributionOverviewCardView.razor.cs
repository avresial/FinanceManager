using ApexCharts;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class ExpenseDistributionOverviewCardView
{
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public List<NameValueResult> Data { get; set; } = [];

    private decimal TotalExpenses => Data.Count == 0 ? 0 : Math.Round(Data.Sum(x => x.Value), 2);

    private readonly ApexChartOptions<NameValueResult> _chartOptions = new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar { Show = false },
            Background = "transparent",
        },
        Legend = new Legend { Show = false },
        Colors = ColorsProvider.GetColors(),
    };

    protected override async Task OnParametersSetAsync()
    {
        _chartOptions.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(CurrencyShortName),
            },
        };

        if (_chart is not null)
            await _chart.UpdateSeriesAsync(true);
    }
}