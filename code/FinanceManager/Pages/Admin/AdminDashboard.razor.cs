using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class AdminDashboard
{
    private int? _userCount = default;
    private int? _accountsCount = default;
    private int? _totalTrackedMoney = default;

    private ChartOptions _chartOptions = new ChartOptions()
    {
        ChartPalette = ["#ffab00"],
        ShowLegend = false,
        ShowToolTips = false,
        ShowLabels = false,
        ShowLegendLabels = false,
    };
    private AxisChartOptions _axisChartOptions = new AxisChartOptions()
    {
        MatchBoundsToSize = true,
    };

    private List<ChartSeries>? _dailyActiveUsersSeries = null;
    private List<ChartSeries>? _newUsersSeries = null;

    [Inject] required public AdministrationUsersService AdministrationUsersService { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _userCount = await AdministrationUsersService.GetUsersCount();
        _accountsCount = await AdministrationUsersService.GetAccountsCount();
        _totalTrackedMoney = await AdministrationUsersService.GetTotalTrackedMoney();
        StateHasChanged();

        await Task.Delay(1000);
        _dailyActiveUsersSeries =
        [
            new ChartSeries() { Name = "Users count", Data = Enumerable.Range(1, 32).Select(x =>  (double)Random.Shared.Next(1, 200)).ToArray() },
        ];
        StateHasChanged();


        await Task.Delay(1000);
        _newUsersSeries =
        [
            new ChartSeries() { Name = "Users count", Data = Enumerable.Range(1, 32).Select(x =>  (double)x*Random.Shared.Next(5, 30)).ToArray() },
        ];
        StateHasChanged();
    }
}