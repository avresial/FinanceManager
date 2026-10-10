using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class Dashboard : ComponentBase
{
    private const int _third = 4;
    private const int _half = 6;
    private const int _full = 12;

    // Display order and large-screen widths (preferred, max) of every card. Time-series charts
    // read well wide; distribution, list and summary cards are capped at half width so they are
    // never stretched. See DashboardCardLayout for how the visible cards fill rows.
    private static readonly DashboardGridCard[] _cards =
    [
        new(DashboardCards.NetWorth, DashboardCards.All.Single(card => card.Id == DashboardCards.NetWorth).Title, new(_full, _full)),
        new(DashboardCards.NetCashFlow, DashboardCards.All.Single(card => card.Id == DashboardCards.NetCashFlow).Title, new(_half, _full)),
        new(DashboardCards.CashFlowForecast, DashboardCards.All.Single(card => card.Id == DashboardCards.CashFlowForecast).Title, new(_third, _half)),
        new(DashboardCards.ClosingBalance, DashboardCards.All.Single(card => card.Id == DashboardCards.ClosingBalance).Title, new(_half, _full)),
        new(DashboardCards.Labels, DashboardCards.All.Single(card => card.Id == DashboardCards.Labels).Title, new(_third, _half)),
        new(DashboardCards.Assets, DashboardCards.All.Single(card => card.Id == DashboardCards.Assets).Title, new(_third, _half)),
        new(DashboardCards.Liabilities, DashboardCards.All.Single(card => card.Id == DashboardCards.Liabilities).Title, new(_third, _half)),
        new(DashboardCards.Expenses, DashboardCards.All.Single(card => card.Id == DashboardCards.Expenses).Title, new(_third, _half)),
        new(DashboardCards.Insights, DashboardCards.All.Single(card => card.Id == DashboardCards.Insights).Title, new(_third, _half)),
        new(DashboardCards.FinancialAlerts, DashboardCards.All.Single(card => card.Id == DashboardCards.FinancialAlerts).Title, new(_third, _half)),
        new(DashboardCards.RecurringTransactions, DashboardCards.All.Single(card => card.Id == DashboardCards.RecurringTransactions).Title, new(_third, _half)),
        new(DashboardCards.TransactionLog, DashboardCards.All.Single(card => card.Id == DashboardCards.TransactionLog).Title, new(_full, _full)),
    ];

    // Cards that render figures for the selected date range; only these show the refresh indicator.
    private static readonly HashSet<string> _rangeDependentCards =
    [
        DashboardCards.NetWorth,
        DashboardCards.NetCashFlow,
        DashboardCards.ClosingBalance,
        DashboardCards.Labels,
        DashboardCards.Assets,
        DashboardCards.Liabilities,
        DashboardCards.Expenses,
    ];

    private DashboardOverviewDto? _overview;
    private bool _isLoading = true;
    private bool _hasError;

    // The date range that the currently-held _overview was loaded for. Cards render
    // these (via DisplayStartDate/DisplayEndDate) rather than the live selection so
    // the displayed period label always matches the displayed amounts, even mid-reload.
    private DateTime _overviewStart;
    private DateTime _overviewEnd;

    // Guards against a slower, earlier reload overwriting a newer date selection:
    // every LoadOverview claims an incrementing version and only commits its result
    // if it is still the latest in-flight request.
    private readonly RefreshVersionGate _overviewGate = new();

    // True while the latest claimed request is running. Only the latest request clears it, so a
    // superseded response can never hide the indicator of the newer one.
    private bool _isRequestInFlight;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    // Dates handed to the cards: while an overview is held they track that overview's
    // range so amounts and period labels stay paired during a reload; with no overview
    // (first paint / self-load fallback) they fall back to the live selection.
    private DateTime DisplayStartDate => _overview is null ? StartDate : _overviewStart;
    private DateTime DisplayEndDate => _overview is null ? EndDate : _overviewEnd;

    // The user asked for a range other than the one the held overview shows and that data is still on
    // its way. Compared by day: the default end is "now", which moves between visits without
    // changing what the user sees.
    private bool IsRefreshing => _overview is not null && _isRequestInFlight
        && (StartDate.Date != _overviewStart.Date || EndDate.Date != _overviewEnd.Date);

    private static bool IsRangeDependent(string cardId) => _rangeDependentCards.Contains(cardId);

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required DashboardHttpClient DashboardHttpClient { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    // Card-specific models mapped from the single overview response. They are null
    // until the first load resolves (and when the overview is unavailable), in which
    // case the cards fall back to their existing standalone self-loading behavior.
    private TimeSeriesCardModel? NetWorthModel => _overview is null ? null : new(_overview.NetWorthSeries);
    private TimeSeriesCardModel? NetCashFlowModel => _overview is null ? null : new(_overview.NetCashFlowSeries);
    private TimeSeriesCardModel? ClosingBalanceModel => _overview is null ? null : new(_overview.ClosingBalanceSeries);
    private DistributionCardModel? LiabilitiesModel => _overview is null ? null : new(_overview.LiabilitiesPerType, _overview.LiabilitiesPerAccount);
    private NameValueListCardModel? LabelsModel => _overview is null ? null : new(_overview.LabelsValue);
    private DistributionCardModel? AssetsModel => _overview is null ? null : new(_overview.AssetsPerType, _overview.AssetsPerAccount);
    private NameValueListCardModel? ExpenseModel => _overview is null ? null : new(_overview.ExpenseDistribution);

    protected override async Task OnInitializedAsync()
    {
        // A rolling last-31-days window rather than the current calendar month, so charts always have
        // ~a month of history — the current-month range is nearly empty on the 1st. #685
        // Shared with the Assets and Liabilities pages so the three cannot drift apart. #700
        var (Start, End) = DateRangeHelper.GetDefaultOverviewRange(DateTime.UtcNow);
        StartDate = Start;
        EndDate = End;

        await LoadOverview();
    }

    private bool IsAutoHiddenWhenEmpty(string cardId) =>
        _overview is not null && DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(cardId, _overview);

    // DashboardDatePicker exposes a synchronous Action callback, so kick off the
    // reload without awaiting; LoadOverview owns its own state and error handling.
    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        StartDate = changed.Start;
        EndDate = changed.End;
        _ = LoadOverview();
    }

    // Stale-while-revalidate: paint the stored snapshot, always re-fetch, and only repaint and
    // re-persist when the fresh overview differs. SnapshotRefreshCoordinator owns that workflow —
    // see docs/architecture/concepts/ui-snapshots.md.
    private async Task LoadOverview()
    {
        // Claimed here rather than inside the coordinator so the same version also guards the
        // logged-out branch below, which commits state before the run starts.
        var requestVersion = _overviewGate.Claim();
        var startDate = StartDate;
        var endDate = EndDate;

        _hasError = false;
        _isRequestInFlight = true;
        StateHasChanged();

        try
        {
            await RefreshOverviewAsync(requestVersion, startDate, endDate);
        }
        finally
        {
            // A superseded run leaves the flag alone: the newer run owns it now.
            if (_overviewGate.IsCurrent(requestVersion))
            {
                _isRequestInFlight = false;
                StateHasChanged();
            }
        }
    }

    private async Task RefreshOverviewAsync(int requestVersion, DateTime startDate, DateTime endDate)
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            if (_overviewGate.IsCurrent(requestVersion))
            {
                _overview = null;
                _isLoading = false;
                StateHasChanged();
            }
            return;
        }

        var currency = await SettingsService.GetCurrencyAsync();

        var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<DashboardOverviewSnapshot, DashboardOverviewDto>
        {
            Key = BuildSnapshotKey(user.UserId, currency.Id),
            Gate = _overviewGate,
            ClaimedVersion = requestVersion,
            ToModel = snapshot => snapshot.ToDto(),
            FetchAsync = () => DashboardHttpClient.GetOverview(user.UserId, currency.Id, startDate, endDate),
            ToSnapshot = DashboardOverviewSnapshot.FromDto,

            // The snapshot renders the range it was captured for; fresh data renders the requested one.
            OnSnapshotPainted = overview => ShowOverview(overview, overview.StartDate, overview.EndDate),
            OnSnapshotMissing = () =>
            {
                _isLoading = true;
                StateHasChanged();
                return Task.CompletedTask;
            },
            OnRefreshed = overview => ShowOverview(overview, startDate, endDate),
        });

        if (!_overviewGate.IsCurrent(requestVersion))
            return;

        // A failed refresh never discards an overview that is already on screen (painted from the
        // snapshot or held from an earlier load): the old data and its period labels stay, and the
        // alert tells the user the requested range did not load and offers Retry. With nothing on
        // screen the same flag is the blocking error state.
        _hasError = result.Outcome == SnapshotRefreshOutcome.Failed;

        _isLoading = false;
        StateHasChanged();
    }

    private Task ShowOverview(DashboardOverviewDto overview, DateTime start, DateTime end)
    {
        _overview = overview;
        _overviewStart = start;
        _overviewEnd = end;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // Per-user key with no date component, so a single snapshot per user is overwritten each save.
    // The key is currency-scoped so a snapshot saved before a preferred-currency change is never
    // painted with the new currency's labels.
    private static string BuildSnapshotKey(int userId, int currencyId) => $"dashboard-overview:{userId}:{currencyId}";

}