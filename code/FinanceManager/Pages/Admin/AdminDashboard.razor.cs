using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class AdminDashboard
{
    private int _userCount = 0;
    private ChartOptions _axisChartOptions = new ChartOptions()
    {
        ChartPalette = ["#ffab00", "#34C759", "#007AFF"],
        ShowLegend = false,
    };

    private List<ChartSeries>? _dailyAcriveUsersSeries = null;
    //new()
    //{
    //    new ChartSeries() { Name = "Users count", Data = new double[] {0,1,2,3,4,5,10,20,30 } },
    //};

    private List<ChartSeries>? _newUsersSeries = new()
    {
        new ChartSeries() { Name = "Users count", Data = new double[] {30,20,10,5,4,3,2,1,0 } },
    };

    [Inject] required public AdministrationUsersService AdministrationUsersService { get; set; }



    protected override async Task OnInitializedAsync()
    {
        var userCount = await AdministrationUsersService.GetUsersCount();
        _userCount = userCount is null ? 0 : userCount.Value;
    }
}