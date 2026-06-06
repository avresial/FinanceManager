using ApexCharts;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminDashboard : ComponentBase
{
    private int? _userCount = default;
    private int? _accountsCount = default;
    private int? _totalTrackedMoney = default;
    private int? _newVisitorsToday = default;

    private static readonly ApexChartOptions<ChartEntryModel> _chartOptions = new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false },
            FontFamily = "Roboto, sans-serif",
        },
        Colors = ["#ffab00"],
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Show = false },
        PlotOptions = new PlotOptions
        {
            Bar = new PlotOptionsBar { BorderRadius = 4, ColumnWidth = "60%" },
        },
        Grid = new Grid
        {
            BorderColor = "rgba(128,128,128,0.18)",
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
        },
        Xaxis = new XAxis
        {
            Type = XAxisType.Category,
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels
            {
                Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
            },
        },
        Yaxis =
        [
            new YAxis
            {
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Labels = new YAxisLabels
                {
                    Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                },
            },
        ],
        Tooltip = new Tooltip { Theme = Mode.Dark },
    };

    private List<ChartEntryModel>? _dailyActiveUsers;
    private List<ChartEntryModel>? _newUsers;

    [Inject] required public AdministrationUsersHttpClient AdministrationUsersHttpClient { get; set; }
    [Inject] required public NewVisitorsHttpClient NewVisitorsHttpClient { get; set; }
    [Inject] required public ILogger<AdminDashboard> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _userCount = await AdministrationUsersHttpClient.GetUsersCount();
            _accountsCount = await AdministrationUsersHttpClient.GetAccountsCount();
            _totalTrackedMoney = await AdministrationUsersHttpClient.GetTotalTrackedMoney();
            _newVisitorsToday = await NewVisitorsHttpClient.GetVisit(DateTime.UtcNow);
            StateHasChanged();

            _dailyActiveUsers = await AdministrationUsersHttpClient.GetDailyActiveUsers();
            StateHasChanged();

            _newUsers = await AdministrationUsersHttpClient.GetNewUsersDaily();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading admin dashboard data.");
            throw;
        }

        StateHasChanged();
    }
}