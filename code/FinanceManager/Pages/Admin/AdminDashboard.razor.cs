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
        ShowLabels = false,
    };

    private List<ChartSeries> _series = new()
    {
        new ChartSeries() { Name = "Users count", Data = new double[] {1,2,3,4,5,10,20,30 } },
    };

    [Inject] required public AdministrationUsersService AdministrationUsersService { get; set; }



    protected override async Task OnInitializedAsync()
    {
        var userCount = await AdministrationUsersService.GetUsersCount();
        _userCount = userCount is null ? 0 : userCount.Value;
    }
}