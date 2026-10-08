using ApexCharts;
using FinanceManager.Components.Shared.Components;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class AssetsDistributionOverviewCardView
{
    private const string _viewByType = DistributionViewToggle.TypeView;
    private const string _viewByAccount = DistributionViewToggle.AccountView;

    private string _view = _viewByType;
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool HasError { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public List<NameValueResult> TypeData { get; set; } = [];
    [Parameter] public List<NameValueResult> WalletData { get; set; } = [];

    private List<NameValueResult> ActiveData => _view == _viewByAccount ? WalletData : TypeData;

    // A single category would render as a one-slice pie that carries no information, so the card shows only its summary row.
    private bool IsSingleCategory => ActiveData.Select(x => x.Name).Distinct().Count() == 1;

    private decimal TotalAssets => TypeData.Count == 0 ? 0 : Math.Round(TypeData.Sum(x => x.Value), 2);

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

        if (_chart is not null && !IsSingleCategory)
            await _chart.UpdateSeriesAsync(true);
    }

    private async Task OnViewChangedAsync(string view)
    {
        _view = view;
        StateHasChanged();
        if (_chart is not null && !IsSingleCategory)
            await _chart.UpdateSeriesAsync(true);
    }
}