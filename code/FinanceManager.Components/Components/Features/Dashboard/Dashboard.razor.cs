using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Components.Features.Dashboard;

public partial class Dashboard : ComponentBase
{
    private const int _unitHeight = 130;

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
    private int _loadOverviewVersion;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    // Dates handed to the cards: while an overview is held they track that overview's
    // range so amounts and period labels stay paired during a reload; with no overview
    // (first paint / self-load fallback) they fall back to the live selection.
    private DateTime DisplayStartDate => _overview is null ? StartDate : _overviewStart;
    private DateTime DisplayEndDate => _overview is null ? EndDate : _overviewEnd;

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required DashboardHttpClient DashboardHttpClient { get; set; }
    [Inject] public required ISnapshotService SnapshotService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<Dashboard> Logger { get; set; }

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
        var (Start, End) = DateRangeHelper.GetCurrentMonthRange();
        StartDate = Start;
        EndDate = End;

        await LoadOverview();
    }

    // DashboardDatePicker exposes a synchronous Action callback, so kick off the
    // reload without awaiting; LoadOverview owns its own state and error handling.
    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        StartDate = changed.Start;
        EndDate = changed.End;
        _ = LoadOverview();
    }

    private async Task LoadOverview()
    {
        var requestVersion = Interlocked.Increment(ref _loadOverviewVersion);
        var startDate = StartDate;
        var endDate = EndDate;

        _hasError = false;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            if (requestVersion == _loadOverviewVersion)
            {
                _overview = null;
                _isLoading = false;
                StateHasChanged();
            }
            return;
        }

        var currency = await SettingsService.GetCurrencyAsync();
        var snapshotKey = BuildSnapshotKey(user.UserId);

        // Paint the last-rendered snapshot immediately so the page feels instant on
        // re-navigation. The API call below always runs and reconciles the view.
        DashboardOverviewSnapshot? snapshot = null;
        try
        {
            snapshot = await SnapshotService.GetAsync<DashboardOverviewSnapshot>(snapshotKey);
        }
        catch (Exception snapshotReadEx)
        {
            // A storage/interop failure on read must not abort the load — fall through to the fresh fetch.
            Logger.LogWarning(snapshotReadEx, "Failed to read dashboard snapshot; continuing with fresh fetch.");
        }

        if (snapshot is not null && requestVersion == _loadOverviewVersion)
        {
            _overview = snapshot.ToDto();
            _overviewStart = snapshot.StartDate;
            _overviewEnd = snapshot.EndDate;
            _isLoading = false;
            StateHasChanged();
        }
        else if (requestVersion == _loadOverviewVersion)
        {
            _isLoading = true;
            StateHasChanged();
        }

        DashboardOverviewDto? freshOverview = null;
        var changed = false;
        try
        {
            freshOverview = await DashboardHttpClient.GetOverview(user.UserId, currency.Id, startDate, endDate);

            // Only repaint and persist when the fresh data actually differs from the
            // snapshot we already rendered — avoids a redundant flush on every visit.
            changed = freshOverview is not null && !IsSameAsSnapshot(freshOverview, snapshot);

            if (requestVersion == _loadOverviewVersion && changed)
            {
                _overview = freshOverview;
                _overviewStart = startDate;
                _overviewEnd = endDate;
            }
        }
        catch (Exception ex)
        {
            // If we already rendered a snapshot, keep it visible rather than blanking the screen.
            if (requestVersion == _loadOverviewVersion && snapshot is null)
            {
                _overview = null;
                _hasError = true;
            }
            Logger.LogError(ex, "Error loading dashboard overview");
        }
        finally
        {
            if (requestVersion == _loadOverviewVersion)
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        // Skip the write when a newer load has superseded this one, so a slower
        // earlier request can't overwrite the latest request's snapshot.
        if (requestVersion == _loadOverviewVersion && freshOverview is not null && changed)
        {
            try
            {
                await SnapshotService.SetAsync(snapshotKey, DashboardOverviewSnapshot.FromDto(freshOverview));
            }
            catch (Exception snapshotEx)
            {
                Logger.LogWarning(snapshotEx, "Dashboard overview loaded but snapshot save failed.");
            }
        }
    }

    // Per-user key with no date component, so a single snapshot per user is overwritten each save.
    private static string BuildSnapshotKey(int userId) => $"dashboard-overview:{userId}";

    private static bool IsSameAsSnapshot(DashboardOverviewDto overview, DashboardOverviewSnapshot? snapshot)
    {
        if (snapshot is null)
            return false;

        // Compare rendered content only; FetchedAtUtc lives on the snapshot, not the DTO.
        return JsonSerializer.Serialize(overview) == JsonSerializer.Serialize(snapshot.ToDto());
    }
}