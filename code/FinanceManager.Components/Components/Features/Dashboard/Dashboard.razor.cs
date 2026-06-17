using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

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
    [Inject] public required DashboardOverviewCacheService DashboardOverviewCacheService { get; set; }
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

        var currency = SettingsService.GetCurrency();

        // Render cached data immediately so the page feels instant on re-navigation.
        // The API call below always runs and updates the view when fresh data arrives.
        var cached = await DashboardOverviewCacheService.GetCachedAsync(user.UserId, currency.Id, startDate, endDate);
        if (cached is not null && requestVersion == _loadOverviewVersion)
        {
            _overview = cached.ToDto();
            _overviewStart = startDate;
            _overviewEnd = endDate;
            _isLoading = false;
            StateHasChanged();
        }
        else if (requestVersion == _loadOverviewVersion)
        {
            _isLoading = true;
            StateHasChanged();
        }

        DashboardOverviewDto? freshOverview = null;
        try
        {
            freshOverview = await DashboardHttpClient.GetOverview(user.UserId, currency.Id, startDate, endDate);

            if (requestVersion == _loadOverviewVersion)
            {
                _overview = freshOverview;
                _overviewStart = startDate;
                _overviewEnd = endDate;
            }
        }
        catch (Exception ex)
        {
            // If we already rendered cached data, keep it visible rather than blanking the screen.
            if (requestVersion == _loadOverviewVersion && cached is null)
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

        if (freshOverview is not null)
        {
            try
            {
                await DashboardOverviewCacheService.SaveAsync(freshOverview);
            }
            catch (Exception cacheEx)
            {
                Logger.LogWarning(cacheEx, "Dashboard overview loaded but caching failed.");
            }
        }
    }
}