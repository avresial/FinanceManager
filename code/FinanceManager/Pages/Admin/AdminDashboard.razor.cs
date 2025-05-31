using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class AdminDashboard
{
    private int? _userCount = default;
    private int? _accountsCount = default;
    private int? _totalTrackedMoney = default;
    private int? _newVisitorsToday = default;

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
    [Inject] required public NewVisitorsService NewVisitorsService { get; set; }


    protected override async Task OnInitializedAsync()
    {
        try
        {
            _userCount = await AdministrationUsersService.GetUsersCount();
            _accountsCount = await AdministrationUsersService.GetAccountsCount();
            _totalTrackedMoney = await AdministrationUsersService.GetTotalTrackedMoney();
            _newVisitorsToday = await NewVisitorsService.GetVisit(DateTime.UtcNow);
            StateHasChanged();

            var dailyActiveUsers = await AdministrationUsersService.GetDailyActiveUsers();
            _dailyActiveUsersSeries =
            [
                new ChartSeries()
                {
                    Name = "Users count",
                    Data = dailyActiveUsers.Select(x =>  (double)x.Value).ToArray()
                },
            ];
            StateHasChanged();

            var newUsers = await AdministrationUsersService.GetNewUsersDaily();
            _newUsersSeries =
            [
                new ChartSeries()
                {
                    Name = "Users count",
                    Data = newUsers.Select(x =>  (double)x.Value).ToArray()
                },
            ];
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw;
        }

        StateHasChanged();
    }
}