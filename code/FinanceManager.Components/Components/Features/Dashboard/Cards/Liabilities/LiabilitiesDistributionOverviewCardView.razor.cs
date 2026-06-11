using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.Liabilities;

public partial class LiabilitiesDistributionOverviewCardView
{
    private const string _viewByType = "type";
    private const string _viewByAccount = "account";

    private string _view = _viewByType;
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public List<NameValueResult> TypeData { get; set; } = [];
    [Parameter] public List<NameValueResult> AccountData { get; set; } = [];

    private List<NameValueResult> ActiveData => _view == _viewByAccount ? AccountData : TypeData;

    // Derive the total from the active dataset so per-view percentages and the header stay self-consistent.
    private decimal TotalLiabilities => ActiveData.Count == 0 ? 0 : Math.Round(ActiveData.Sum(x => x.Value), 2);

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

    private async Task OnViewChangedAsync(string view)
    {
        _view = view;
        StateHasChanged();
        if (_chart is not null)
            await _chart.UpdateSeriesAsync(true);
    }
}