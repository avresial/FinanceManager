using ApexCharts;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Security.Claims;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminDashboard : ComponentBase
{
    private int? _userCount = default;
    private int? _accountsCount = default;
    private int? _totalTrackedMoney = default;
    private int? _newVisitorsToday = default;

    private readonly ApexChartOptions<ChartEntryModel> _chartOptions = CreateChartOptions();
    private readonly ApexChartOptions<ChartEntryModel> _newUsersChartOptions = CreateChartOptions();

    private static ApexChartOptions<ChartEntryModel> CreateChartOptions() => new()
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
    private readonly RefreshVersionGate _newUsersGate = new();
    private int _newUsersChartVersion;

    [Inject] required public AdministrationUsersHttpClient AdministrationUsersHttpClient { get; set; }
    [Inject] required public NewVisitorsHttpClient NewVisitorsHttpClient { get; set; }
    [Inject] required public ILogger<AdminDashboard> Logger { get; set; }
    [Inject] required public ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] required public AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    /// <summary>Paints the admin chart snapshot while refreshing it and loading the other dashboard metrics.</summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(ClaimTypes.Sid)?.Value;
            if (authState.User.Identity?.IsAuthenticated != true || !authState.User.IsInRole("Admin")
                || string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("An authenticated admin user is required.");

            await Task.WhenAll(RefreshNewUsersAsync(userId), LoadMetricsAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading admin dashboard data.");
            throw;
        }
    }

    private async Task LoadMetricsAsync()
    {
        _userCount = await AdministrationUsersHttpClient.GetUsersCount();
        _accountsCount = await AdministrationUsersHttpClient.GetAccountsCount();
        _totalTrackedMoney = await AdministrationUsersHttpClient.GetTotalTrackedMoney();
        _newVisitorsToday = await NewVisitorsHttpClient.GetVisit(DateTime.UtcNow);
        StateHasChanged();

        _dailyActiveUsers = await AdministrationUsersHttpClient.GetDailyActiveUsers();
        StateHasChanged();
    }

    private async Task RefreshNewUsersAsync(string userId)
    {
        var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<NewUsersSnapshot, List<ChartEntryModel>>
        {
            Key = $"new-users-chart-{userId}",
            Gate = _newUsersGate,
            ToModel = snapshot => snapshot.Entries,
            FetchAsync = async () => await AdministrationUsersHttpClient.GetNewUsersDaily(),
            ToSnapshot = model => new NewUsersSnapshot { Entries = model },
            OnSnapshotPainted = PaintNewUsersAsync,
            OnRefreshed = PaintNewUsersAsync,
        });

        if (result.IsBlockingFailure)
            throw result.Error!;
    }

    private Task PaintNewUsersAsync(List<ChartEntryModel> model)
    {
        _newUsers = model;
        // ApexCharts must rebuild its series when the rendered content changes.
        _newUsersChartVersion++;
        StateHasChanged();
        return Task.CompletedTask;
    }
}