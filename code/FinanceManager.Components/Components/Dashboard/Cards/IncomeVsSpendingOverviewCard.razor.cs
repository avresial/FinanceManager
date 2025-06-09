using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class IncomeVsSpendingOverviewCard
{

    private TimeSpan _timeLabelSpacing = TimeSpan.FromDays(1);
    private bool _isInitializing = true;
    private bool _isLoading = false;
    private List<TimeSeriesChartSeries> _series = [];

    private readonly AxisChartOptions _axisChartOptions = new()
    {
        MatchBoundsToSize = true,
    };
    private readonly ChartOptions _chartOptions = new()
    {
        MaxNumYAxisTicks = 5,

        YAxisLines = false,
        YAxisRequireZeroPoint = true,
        XAxisLines = false,
        LineStrokeWidth = 3,
        ChartPalette = ["#B2BF84", "#D93D3D", "#ffab00"],
        ShowLegend = false,
    };

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public bool DisplayIncome { get; set; }
    [Parameter] public bool DisplaySpending { get; set; }
    [Parameter] public bool DisplayBalance { get; set; }
    [Parameter] public bool UseOnlyPrimaryColor { get; set; }

    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required ILogger<IncomeVsSpendingOverviewCard> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        if (UseOnlyPrimaryColor) _chartOptions.ChartPalette = ["#ffab00"];

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _series.Clear();
            return;
        }
        var timespanInDays = (DateTime.UtcNow - StartDateTime).TotalDays;

        if (timespanInDays < 32)
            _timeLabelSpacing = TimeSpan.FromDays(7);
        else if (timespanInDays < 90)
            _timeLabelSpacing = TimeSpan.FromDays(31);
        else
            _timeLabelSpacing = TimeSpan.FromDays(90);

        TimeSpan chartTimeSpan = TimeSpan.FromDays(1);
        List<TimeSeriesChartSeries> newData = [];
        try
        {
            if (DisplayIncome)
            {
                newData.Add(new TimeSeriesChartSeries
                {
                    Index = 0,
                    Name = "Income",
                    Data = (await MoneyFlowService.GetIncome(user.UserId, StartDateTime.Date, DateTime.UtcNow, chartTimeSpan))
                    .Select(x => new TimeSeriesChartSeries.TimeValue(x.DateTime, (double)x.Value)).ToList(),
                    IsVisible = true,
                    LineDisplayType = LineDisplayType.Line,
                    DataMarkerTooltipTitleFormat = "{{X_VALUE}}",
                    DataMarkerTooltipSubtitleFormat = "{{Y_VALUE}}"
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplaySpending)
            {
                newData.Add(new TimeSeriesChartSeries
                {
                    Index = 0,
                    Name = "Spending",
                    Data = (await MoneyFlowService.GetSpending(user.UserId, StartDateTime.Date, DateTime.UtcNow, chartTimeSpan))
                    .Select(x => new TimeSeriesChartSeries.TimeValue(x.DateTime, (double)x.Value)).ToList(),
                    IsVisible = true,
                    LineDisplayType = LineDisplayType.Line,
                    DataMarkerTooltipTitleFormat = "{{X_VALUE}}",
                    DataMarkerTooltipSubtitleFormat = "{{Y_VALUE}}"
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplayBalance)
            {
                newData.Add(new TimeSeriesChartSeries
                {
                    Index = 0,
                    Name = "Balance",
                    Data = (await MoneyFlowService.GetBalance(user.UserId, StartDateTime.Date, DateTime.UtcNow, chartTimeSpan))
                    .Select(x => new TimeSeriesChartSeries.TimeValue(x.DateTime, (double)x.Value)).ToList(),
                    IsVisible = true,
                    LineDisplayType = LineDisplayType.Line,
                    DataMarkerTooltipTitleFormat = "{{X_VALUE}}",
                    DataMarkerTooltipSubtitleFormat = "{{Y_VALUE}}"
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        _series = newData;
        _isLoading = false;
        _isInitializing = false;
    }
}