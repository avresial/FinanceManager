using ApexCharts;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.Administration.Models;
using FinanceManager.Components.Features.Administration.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminDashboard : ComponentBase
{
    private int? _userCount = default;
    private int? _accountsCount = default;
    private int? _totalTrackedMoney = default;
    private int? _newVisitorsToday = default;

    // Guards the accounts count card against a slower, earlier reload overwriting a newer one.
    private readonly RefreshVersionGate _accountsCountGate = new();

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
    [Inject] required public ILoginService LoginService { get; set; }
    [Inject] required public AdminDashboardSnapshotStore SnapshotStore { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadAccountsCount();

            _userCount = await AdministrationUsersHttpClient.GetUsersCount();
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

    // Stale-while-revalidate for the accounts count card: paint the stored value immediately, always
    // re-fetch, and only repaint when the fresh count differs. The coordinator owns that workflow —
    // see docs/codebase/UI-SNAPSHOTS.md. Runs first so the snapshot paints before any HTTP round-trip.
    private async Task LoadAccountsCount()
    {
        var version = _accountsCountGate.Claim();
        var user = await LoginService.GetLoggedUser();
        if (!_accountsCountGate.IsCurrent(version)) return;

        // The page requires an authenticated admin; without one there is nothing to scope the snapshot to.
        if (user is null)
            return;

        await SnapshotStore.RefreshAccountsCountAsync(
            user.UserId,
            _accountsCountGate,
            version,
            () => AdministrationUsersHttpClient.GetAccountsCount(),
            onSnapshotPainted: ShowAccountsCount,
            onRefreshed: ShowAccountsCount);
    }

    private Task ShowAccountsCount(AdminAccountsCountCardModel model)
    {
        // The coordinator only invokes the callbacks for a run that is still current.
        _accountsCount = model.Count;
        StateHasChanged();
        return Task.CompletedTask;
    }
}