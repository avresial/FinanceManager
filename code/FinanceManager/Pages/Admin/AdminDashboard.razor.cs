using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using FinanceManager.Components.HttpContexts;

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

    [Inject] required public AdministrationUsersHttpContext AdministrationUsersHttpContext { get; set; }
    [Inject] required public NewVisitorsHttpContext NewVisitorsHttpContext { get; set; }


    protected override async Task OnInitializedAsync()
    {
        try
        {
            _userCount = await AdministrationUsersHttpContext.GetUsersCount();
            _accountsCount = await AdministrationUsersHttpContext.GetAccountsCount();
            _totalTrackedMoney = await AdministrationUsersHttpContext.GetTotalTrackedMoney();
            _newVisitorsToday = await NewVisitorsHttpContext.GetVisit(DateTime.UtcNow);
            StateHasChanged();

            var dailyActiveUsers = await AdministrationUsersHttpContext.GetDailyActiveUsers();
            _dailyActiveUsersSeries =
            [
                new ChartSeries()
                {
                    Name = "Users count",
                    Data = dailyActiveUsers.Select(x =>  (double)x.Value).ToArray()
                },
            ];
            StateHasChanged();

            var newUsers = await AdministrationUsersHttpContext.GetNewUsersDaily();
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