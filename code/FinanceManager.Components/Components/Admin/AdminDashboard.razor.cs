using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Charts;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminDashboard : ComponentBase
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
    };
    private List<ChartSeries<double>>? _dailyActiveUsersSeries = null;
    private List<ChartSeries<double>>? _newUsersSeries = null;

    [Inject] required public AdministrationUsersHttpClient AdministrationUsersHttpClient { get; set; }
    [Inject] required public NewVisitorsHttpClient NewVisitorsHttpClient { get; set; }


    protected override async Task OnInitializedAsync()
    {
        try
        {
            _userCount = await AdministrationUsersHttpClient.GetUsersCount();
            _accountsCount = await AdministrationUsersHttpClient.GetAccountsCount();
            _totalTrackedMoney = await AdministrationUsersHttpClient.GetTotalTrackedMoney();
            _newVisitorsToday = await NewVisitorsHttpClient.GetVisit(DateTime.UtcNow);
            StateHasChanged();

            var dailyActiveUsers = await AdministrationUsersHttpClient.GetDailyActiveUsers();
            _dailyActiveUsersSeries =
            [
                new ChartSeries<double>()
                {
                    Name = "Users count",
                    Data = dailyActiveUsers.Select(x =>  (double)x.Value).ToArray()
                },
            ];
            StateHasChanged();

            var newUsers = await AdministrationUsersHttpClient.GetNewUsersDaily();
            _newUsersSeries =
            [
                new ChartSeries<double>()
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